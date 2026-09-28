# Sandbox Architecture

The server orchestrates ephemeral sandboxes for each pipeline run: one per repo and
toolchain image. A sandbox runs an upstream toolchain image (e.g.
`mcr.microsoft.com/dotnet/sdk:8.0`) with `AgentSmith.Sandbox.Agent` injected as the
entry-point via an init container. Server and agent communicate via Redis — no
`kubectl exec`, no SSH.

## Pieces

- **Server-Pod** — `AgentSmith.Server` orchestrates the pipeline. Creates the
  sandboxes after `ScopeRepos` has decided which repos the run touches, pushes Steps
  to Redis, reads Results.
- **Sandbox-Pod** — runs the user-selected toolchain image with the agent binary
  mounted via shared `emptyDir`. The agent polls Redis, executes Steps, streams
  events back.
- **Wire** — `AgentSmith.Sandbox.Wire` defines the Step / StepEvent / StepResult
  records and the Redis key conventions. Both Server and Agent reference Wire
  so the on-the-wire format has a single source of truth.

## Step kinds

| Kind | Purpose | Notes |
| ---- | ------- | ----- |
| `Run` | Execute a shell command | `run_command` wraps in `sh -c` |
| `ReadFile` | Read a UTF-8 file | 1 MB cap, binary content rejected |
| `WriteFile` | Atomic temp+rename write | 10 MB cap |
| `ListFiles` | Enumerate file-system entries | 1000-entry cap, MaxDepth supported |
| `Grep` | Regex search across a directory | 200-match cap default, ripgrep when present + managed fallback |
| `DirectoryTree` | Nested listing of a directory | Standard noisy directories skipped |
| `Shutdown` | Graceful agent termination | Server pushes on Sandbox dispose |

An agent that receives a kind it doesn't know (an older agent, a newer server)
answers with a result naming the protocol mismatch instead of exiting, so the
server can report the skew.

Limits live in `AgentSmith.Sandbox.Wire/SizeLimits.cs` so Agent and InProcessSandbox
enforce identical numbers.

## Pod spec

```text
Pod (RestartPolicy=Never)
│   labels: app, pipeline-id, owner (this deployment), run-id, conversation-id
│   imagePullSecrets: sandbox.image_pull_secrets
├── initContainer agent-loader     image: <agent_registry>/agent-smith-sandbox-agent:<tag>
│   args: --inject /shared/agent
│   volumeMounts: /shared
└── container toolchain            image: <toolchain image>
    command: [/shared/agent]
    args: --redis-url $REDIS_URL --job-id $JOB_ID [--run-id $RUN_ID]
    env: REDIS_URL, JOB_ID, GIT_TOKEN and sandbox.secrets.env (secretKeyRef)
    volumeMounts: /shared (ro), /work, sandbox.secrets.files (ro)
    workingDir: /work
```

The agent image tag is derived from the release the server is, unless
`sandbox.agent_version` (or `deployment.version`) pins one. A pin is reported as an
advisory finding, never refused, and the installation page shows per project whether
the tag was derived or pinned. Image pull secrets and `sandbox.secrets` are
Kubernetes-only; see [Kubernetes](../../host-it/kubernetes.md#sandbox-pods).

The pod-level `securityContext.fsGroup=1000` makes `/shared/agent` group-readable
+ executable from non-root toolchain images (e.g. `node:20`). Operators with
unusual UIDs override via `SandboxSpec.SecurityContext`.

## Three backends

`SandboxServiceCollectionExtensions.AddSandbox()` (Server) auto-detects:

1. `SANDBOX_TYPE=kubernetes` or `KUBERNETES_SERVICE_HOST` set → `KubernetesSandboxFactory`
2. `SANDBOX_TYPE=docker` or `/var/run/docker.sock` exists → `DockerSandboxFactory`
   (mirrors the K8s shape: `agent-loader` container exits, then a `toolchain`
   container starts with two named volumes — shared agent binary RO, work tree RW)
3. Otherwise → `InProcessSandbox` (no container isolation — single-tenant
   developer machine). The CLI always uses this backend.

`DOCKER_HOST` overrides the default socket URI when set.

> ⚠️ **Detection caveat**: `KUBERNETES_SERVICE_HOST` is set in *every* pod
> (including dev / debug pods). Operators in unusual environments should set
> `SANDBOX_TYPE` explicitly.

## Lifecycle

1. `ScopeRepos` narrows the project to the repos the ticket touches.
2. For each repo the coordinator reads its contexts and resolves a toolchain image per
   context, in this order: `projects.<name>.sandbox.toolchain_image`,
   `projects.<name>.sandbox.images.<language>`, the context's `stack.image` (only if it
   passes `sandbox.allowed_registries`), a built-in per-language table, and a generic
   image with git and no toolchain. One log line names the link that decided.
3. Contexts of one repo that resolve to the same image share one sandbox, sized to the
   largest resource envelope among them. A different image means a separate sandbox.
   The capacity footprint is computed from the same grouping.
4. `ISandboxFactory.CreateAsync` creates the sandbox and waits for it to be ready.
5. `CheckoutSource` clones inside the sandbox. An image without git fails here with
   an error saying so.
6. Before a declared `verify` stage runs, its binary is looked up on the image's
   `PATH`; a missing one is a warning naming image, binary, stage and context.
7. Handlers push their Steps through the sandbox. A `run_command` without its own
   timeout gets `run_command_timeout_seconds` (default 300); a command may ask for
   more, up to `step_timeout_seconds` (default 900), which caps every step. A command
   killed at the cap is reported as timed out.
8. `await using` triggers `DisposeAsync` at pipeline end → Shutdown step + 10 s
   grace + pod delete.

A server crash can leave sandboxes behind. On Kubernetes a corpse reaper deletes this
deployment's pods whose run is no longer live, on a timer and at admission; on Docker
the orphan reaper does the same for containers. Both select by an owner label, whose
value is derived from the Redis endpoint (or set with `SANDBOX_OWNER_ID`), so two
deployments sharing a host or namespace never touch each other's sandboxes.

## Read-only source sandboxes

A design conversation in the dashboard, and a project template a run reads, reach
repositories through read-only source sandboxes instead of run sandboxes. One is
created lazily on the first read, on a generic git-bearing image, and clones one branch
at one commit (or a named revision). Content reads (`ReadFile`, `ListFiles`, `Grep`,
`DirectoryTree`) are served. A process step is served only when the server itself built
it (the clone, a file search, an HTTP transfer); a model-authored command never reaches
a shell. Writes are refused everywhere.

A design conversation holds its source sandboxes between turns for `sandbox.hold_seconds`
(default 180, `0` holds nothing; per project `projects.<name>.sandbox.hold_seconds`;
`SANDBOX_HOLD_SECONDS` when the configuration store names none), so only the first
message pays for the spawn and clone. The reapers spare a held sandbox, and every
capacity check releases holds before it probes, so a hold never costs a run its slot.

## RBAC

The Server's `ServiceAccount` needs:

- `pods` — `create`, `delete`, `get`, `list`, `watch`
- `pods/log` — `get`
- `pods/status` — `get`

`pods/exec` is **not required**. See [`deploy/k8s/2-rbac.yaml`](https://github.com/holgerleichsenring/agent-smith/blob/main/deploy/k8s/2-rbac.yaml).

## Stream bounds

`StreamLimits.EventStreamMaxLength = 10_000` (Wire). Agent's `RedisEventChannel`
sends every `XADD` with `MAXLEN ~` so a single chatty step (`npm install`,
verbose builds) cannot balloon a stream past ~10500 events. Combined with
`DEL`-on-dispose this caps both per-step and per-pipeline Redis pressure.

## Known limitations

- **Mid-step cancellation** — pod-delete works as a hammer; there is no granular
  cancel of a running step.
- **`LocalSourceProvider` in Kubernetes** — throws `NotSupportedException` with an
  operator-facing message. The sandbox pod runs on a different node / filesystem, so
  a host-disk source is unreachable. Use a remote source provider instead.

See [sandbox-agent.md](./sandbox-agent.md) for the Agent-side view.
