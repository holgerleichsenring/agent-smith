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
| `Grep` | Regex search across a directory | 200-match cap default, ripgrep when present + managed fallback |
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

1. `SANDBOX_TYPE=kubernetes` or `KUBERNETES_SERVICE_HOST` set → `KubernetesSandboxFactory`
2. `SANDBOX_TYPE=docker` or `/var/run/docker.sock` exists → `DockerSandboxFactory`
   (mirrors the K8s shape: `agent-loader` container exits, then a `toolchain`
   container starts with two named volumes — shared agent binary RO, work tree RW)
3. Otherwise → `InProcessSandbox` (CLI mode, no container isolation — single-tenant
   developer machine)

`DOCKER_HOST` overrides the default socket URI when set.

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

## What runs where (post p0117)

| Operation | Runs in | Note |
| --------- | ------- | ---- |
| Source clone | Sandbox + server-side LibGit2Sharp (hybrid) | Server-side checkout still feeds 15+ file-reading handlers (Bootstrap*/Load*/Compile*/Analyze*) |
| AI tool calls (read/write/list/grep/run) | Sandbox via `SandboxToolHandler` Steps | Mirrors the K8s/Docker `/work` view |
| Test execution | Sandbox via `dotnet test --logger trx --results-directory /work/test-results` | TRX result-files parsed via `TrxResultParser` into structured `TrxSummary` |
| Commit + push | Sandbox via `SandboxGitOperations` | Captures the `/work` modifications. `CommitAndPRHandler`, `PersistWorkBranchHandler`, `InitCommitHandler` all migrated |
| PR creation | Server-side via Octokit / GitLab REST / AzDO API | API call, no git plumbing |
| Stream cleanup on dispose | Server-side `SandboxRedisChannel.DisposeAsync` | DELs `sandbox:{jobId}:in/events/results`. Best-effort: never throws |

## Stream bounds

`StreamLimits.EventStreamMaxLength = 10_000` (Wire). Agent's `RedisEventChannel`
sends every `XADD` with `MAXLEN ~` so a single chatty step (`npm install`,
verbose builds) cannot balloon a stream past ~10500 events. Combined with
`DEL`-on-dispose this caps both per-step and per-pipeline Redis pressure.

## Known limitations

- **Mid-step cancellation** — pod-delete works as a hammer; granular cancel
  comes later.
- **`Repository.LocalPath` semantics** — still server-side path. Migration to a
  pure-`/work` model needs ~15 file-reading handlers (Bootstrap*/Load*/Compile*/
  Analyze*) to route their reads through `ISandbox`. Bigger than p0117 scope.
- **Helm chart** — deployment still via `deploy/k8s/` flat YAMLs. Helm-ifying
  the manifests is a separate phase.
- **Crash-time Redis-key reaper** — if the Server-Pod crashes mid-pipeline,
  the K8s `OwnerReference` deletes the Sandbox-Pod but the Redis keys for
  that job remain until a `SCAN`-based reaper hosted-service ships.

See [sandbox-agent.md](./sandbox-agent.md) for the Agent-side view.
