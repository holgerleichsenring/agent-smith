# Spawned Jobs

Queue-driven pipeline runs (webhook + poll) execute in **ephemeral CLI containers** spawned per run, not in-process inside the Server pod. The Server is orchestration: it polls, claims, queues, dispatches, and tracks lifecycle. The actual pipeline — git operations, agentic LLM rounds, test execution — runs in a per-run container that has the project's toolchain (dotnet SDK, git, etc.).

This page documents the cross-process flow, the recovery story, and the contracts between the two halves.

## Why ephemeral containers

The Server pod is fundamentally a dispatcher. Running pipelines in-process means its container needs every language's toolchain (dotnet SDK, npm, python, …) for steps like `TestCommand`. That doesn't scale across a multi-language platform — and the architectural intent has always been ephemeral job containers per run. The chat-intent path (Slack/Teams) already uses `IJobSpawner` for this; **p0113** closes the gap on the queue path.

## The two halves

```
┌──────────────────────────── Server pod ──────────────────────────┐
│                                                                  │
│   webhook / poll                                                 │
│        │                                                         │
│        ▼                                                         │
│   TicketClaimService ──── Pending → Enqueued ───────► Redis      │
│        │                                              queue      │
│        │                                              │          │
│        ▼                                              ▼          │
│   PipelineQueueConsumer ◄──────────────── dequeues ──┘          │
│        │                                                         │
│        ▼                                                         │
│   IPipelineJobDispatcher                                         │
│   (JobSpawnerPipelineDispatcher)                                 │
│        │                                                         │
│        ├── 1. SaveAsync(jobId, request) ──► Redis (1h TTL)       │
│        │                                                         │
│        └── 2. SpawnQueueJobAsync(jobId, redisUrl, configPath)    │
│                                       │                          │
└───────────────────────────────────────┼──────────────────────────┘
                                        │ (Docker run / K8s Job)
                                        ▼
┌──────────────────────────── CLI container ──────────────────────┐
│                                                                  │
│   agent-smith run-claimed-job                                    │
│       --job-id <id>                                              │
│       --redis-url <url>                                          │
│       --config /app/config/agentsmith.yml                        │
│                                                                  │
│   1. Connect to Redis                                            │
│   2. IPipelineRequestStore.LoadAsync(jobId) ──► PipelineRequest  │
│   3. ExecutePipelineUseCase.ExecuteAsync(request, configPath)    │
│   4. Lifecycle: Enqueued → InProgress → Done | Failed            │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

## Why Redis-handoff (not CLI args)

`PipelineRequest` carries structured fields: `ProjectName`, `PipelineName`, `TicketId`, `IsInit`, `Headless`, plus a `Context` dictionary that some flows populate (e.g. `ScanBranch`). Stuffing this through CLI args is fragile — escaping issues, shell quoting on different platforms, length limits, and the `Context` dictionary doesn't have a clean argv encoding.

The Server stores the request as JSON under `agentsmith:pipeline-request:{jobId}` with a 1h TTL. The container's CLI args stay short — just the jobId, the Redis URL, and the config path. The container loads the structured request and calls the **structured** `ExecutePipelineUseCase.ExecuteAsync(PipelineRequest, ...)` overload directly — bypassing the intent parser that the Slack-flow uses.

The 1h TTL is a soft contract: spawn must complete and the container must load the request within the hour. Long-running pipelines are unaffected — they've already moved past the load-and-discard phase by then.

## Lifecycle across the boundary

| Transition | Side | Detail |
|------------|------|--------|
| Pending → Enqueued | Server (`TicketClaimService`) | SETNX claim-lock; atomic per ticket |
| Enqueued → InProgress | **Container** (via `PipelineExecutor` inside `ExecutePipelineUseCase`) | First lifecycle write inside the spawned container |
| InProgress → Done / Failed | **Container** (via `LifecycleScope.Dispose`) | At pipeline end |

Between the Server's `Enqueued` write and the container's `InProgress` write sits the **spawn window** — if `SpawnQueueJobAsync` succeeds but the container fails to start (image pull error, K8s quota, Docker daemon down), or starts but never reaches the lifecycle code (config load throws, DI build fails), the ticket would stay `Enqueued` forever without intervention.

`EnqueuedReconciler` (existing, p0095c) is the load-bearing recovery: it sweeps `Enqueued` tickets without active heartbeat back to `Pending` after the configured stale-window (default 10 min). That window is short enough for actionable recovery, long enough that slow-starting containers don't get reset mid-spawn.

## What this MVP ships (p0113a)

- **`IPipelineJobDispatcher`** (Application/Contracts) — the abstraction PipelineQueueConsumer depends on
- **`JobSpawnerPipelineDispatcher`** (Server) — generates jobId, persists request via `IPipelineRequestStore`, calls `IJobSpawner.SpawnQueueJobAsync`
- **`IPipelineRequestStore` + `RedisPipelineRequestStore`** — the Redis-backed handoff
- **`IJobSpawner.SpawnQueueJobAsync`** — added to both `DockerJobSpawner` and `KubernetesJobSpawner`
- **`run-claimed-job`** — hidden CLI subcommand the spawned container runs
- **`PipelineQueueConsumer`** — switched from in-process `ExecutePipelineUseCase` invocation to `IPipelineJobDispatcher.DispatchAsync`

## What is deferred to p0113b

- **`SecretsForwardingPolicy`** whitelist — explicit env-var allow-list crossing the spawn boundary. Today: hard-coded list inside each spawner.
- **`JobRequest.ImageOverride` resolution chain** (`request → pipeline → default`) for projects needing per-language toolchain images.
- **Heartbeat-gap mitigation**: write the first heartbeat **before** the DI build inside `run-claimed-job` so a DI-build crash doesn't ghost the ticket. Today: relies on `EnqueuedReconciler` to revert.
- **`AGENTSMITH_TEST_FAIL_AFTER` env-var fail-injection** for reproducible CI of the persist+recovery paths.
- **Server Dockerfile**: drop SDK if any remains (already aspnet:8.0); CLI Dockerfile remove `p4x` TODO comment.

## Backpressure

`PipelineQueueConsumer` keeps its `SemaphoreSlim` bounded by `QueueConfig.MaxParallel` (default 4). Post-refactor, this gates **concurrent dispatch calls** (each fast: one SETEX + one spawn API call), not in-flight pipeline count. The real binding constraint downstream is LLM-deployment quota, which scales with operator-side configuration (Azure OpenAI deployment, provider rate limits) — agent-smith does not enforce that bound centrally.

## Related

- [Ticket Lifecycle](ticket-lifecycle.md) — the full state machine and what survives what
- [Pipeline System](pipeline-system.md) — command/handler model
