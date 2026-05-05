# Sandbox Architecture

The Server-Pod orchestrates an ephemeral Sandbox-Pod per pipeline run. The
Sandbox-Pod runs an upstream toolchain image (e.g. `mcr.microsoft.com/dotnet/sdk:8.0`)
with `AgentSmith.Sandbox.Agent` injected as the entry-point via an init container.
The Server and Agent communicate via Redis — no `kubectl exec`, no SSH.

## Pieces

- **Server-Pod** — `AgentSmith.Server` orchestrates the pipeline. Creates the
  Sandbox-Pod at pipeline-start, pushes Steps to Redis, reads Results.
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
| `Shutdown` | Graceful agent termination | Server pushes on Sandbox dispose |

Limits live in `AgentSmith.Sandbox.Wire/SizeLimits.cs` so Agent and InProcessSandbox
enforce identical numbers.

## Pod spec

```text
Pod (RestartPolicy=Never)
├── initContainer agent-loader     image: agent-smith-sandbox-agent:latest
│   args: --inject /shared/agent
│   volumeMounts: /shared
└── container toolchain            image: <user toolchain>
    command: [/shared/agent]
    args: --redis-url $REDIS_URL --job-id $JOB_ID
    env: REDIS_URL, JOB_ID, GIT_TOKEN (from secretKeyRef when configured)
    volumeMounts: /shared (ro), /work
    workingDir: /work
```

The pod-level `securityContext.fsGroup=1000` makes `/shared/agent` group-readable
+ executable from non-root toolchain images (e.g. `node:20`). Operators with
unusual UIDs override via `SandboxSpec.SecurityContext`.

## Three backends

`SandboxServiceCollectionExtensions.AddSandbox()` (Server) auto-detects:

1. `SANDBOX_TYPE=kubernetes` or `kubernetes-service-host` set → `KubernetesSandboxFactory`
2. Otherwise → `InProcessSandbox` (CLI mode, no container isolation — single-tenant
   developer machine)

The Docker backend is on the roadmap for a follow-up phase; today, dev-loop runs
through `InProcessSandbox`, prod runs through Kubernetes.

> ⚠️ **Detection caveat**: `KUBERNETES_SERVICE_HOST` is set in *every* pod
> (including dev / debug pods). Operators in unusual environments should set
> `SANDBOX_TYPE` explicitly.

## Lifecycle

1. `PipelineExecutor.ExecuteAsync` checks whether the pipeline contains
   `CheckoutSource / AgenticExecute / Test / GenerateTests / GenerateDocs`.
2. If yes, `SandboxSpecBuilder` resolves the toolchain image from
   `ProjectMap.PrimaryLanguage` (or `ProjectConfig.Sandbox.ToolchainImage`).
3. `ISandboxFactory.CreateAsync` creates the pod and waits for it to be Ready.
4. `CheckoutSourceHandler` (V1 hybrid) clones server-side AND pushes a `git clone`
   Step into the sandbox so `/work` is populated.
5. `AgenticExecuteHandler` / `TestHandler` push their Steps via the sandbox.
6. `await using` triggers `DisposeAsync` at pipeline-end → Shutdown step + 10 s
   grace + `DeleteNamespacedPodAsync`. Belt-and-suspenders: `OwnerReference`
   triggers K8s GC if the Server crashes mid-pipeline.

## RBAC

The Server's `ServiceAccount` needs:

- `pods` — `create`, `delete`, `get`, `list`, `watch`
- `pods/log` — `get`
- `pods/status` — `get`

`pods/exec` is **not required**. See [`deploy/k8s/2-rbac.yaml`](https://github.com/holgerleichsenring/agent-smith/blob/main/deploy/k8s/2-rbac.yaml).

## V1 limitations (deferred to p0117)

- **Docker backend** — not yet implemented.
- **`GenerateTestsHandler` / `GenerateDocsHandler`** — still server-side
  Process.Start. Sandbox migration deferred along with the Docker work.
- **TRX result parsing** — `TestHandler` routes by exit-code only.
- **Branch / commit / push** — still server-side via LibGit2Sharp. Repository
  state is mirrored: server-side `Repository.LocalPath` for commits, sandbox
  `/work` for build/test/exec.
- **Mid-step cancellation** — pod-delete works as a hammer; granular cancel
  comes later.

See [sandbox-agent.md](./sandbox-agent.md) for the Agent-side view.
