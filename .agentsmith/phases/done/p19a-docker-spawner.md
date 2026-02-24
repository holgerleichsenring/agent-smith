# Phase 19a: Docker Compose — Fully Self-Contained (No K8s)

## Goal

Make Docker Compose a fully autonomous local development environment.
No Kubernetes required. No `host.docker.internal` hacks. No mixed-mode operation.

The Dispatcher spawns Agent containers via the **Docker socket** instead of the K8s API.
Everything runs in the same Compose network → `redis:6379` works natively everywhere.

---

## The Problem Today

```
Docker Compose:  redis + dispatcher
Kubernetes:      agentsmith-job (ephemeral)
Issue:           Redis is unreachable from K8s pods
Workaround:      K8S_REDIS_URL=host.docker.internal:6379  ← dirty hack
```

---

## The Solution

```
Docker Compose:  redis + dispatcher + agentsmith-job (spawned via Docker socket)
Kubernetes:      not involved at all in local mode
Result:          redis:6379 works for everyone in the same network
```

---

## New: IJobSpawner Interface

The current `JobSpawner.cs` is tightly coupled to Kubernetes.
We extract a clean interface so the Dispatcher is spawner-agnostic:

```csharp
namespace AgentSmith.Dispatcher.Services;

public interface IJobSpawner
{
    /// <summary>
    /// Spawns an ephemeral agent job for the given intent.
    /// Returns the jobId that can be used to track progress via Redis Streams.
    /// </summary>
    Task<string> SpawnAsync(FixTicketIntent intent, CancellationToken cancellationToken = default);
}
```

Two implementations:

| Class | When | How |
|---|---|---|
| `KubernetesJobSpawner` | `SPAWNER_TYPE=kubernetes` | K8s BatchV1 API (existing logic) |
| `DockerJobSpawner` | `SPAWNER_TYPE=docker` | Docker Engine API via Docker.DotNet |

---

## DockerJobSpawner

Uses the `Docker.DotNet` NuGet package to call the Docker socket.

```csharp
// Key behaviours:
// - Connects to unix:///var/run/docker.sock
// - Runs agentsmith:latest with identical args as the K8s Job
// - Joins the same Docker network as the dispatcher (auto-detected)
// - AutoRemove = true  →  container cleans itself up (replaces K8s TTL)
// - Environment variables injected directly (no K8s Secret needed locally)
```

Container args are identical to the K8s Job:
```
--headless
--job-id   <jobId>
--redis-url redis:6379        ← works! same network
--platform  slack
--channel-id <channelId>
fix #<ticketId> in <project>
```

Environment variables are read from the Dispatcher's own environment
and forwarded directly to the container — no K8s Secret required locally.

---

## SPAWNER_TYPE Environment Variable

```bash
SPAWNER_TYPE=docker      # local Docker Compose mode  (new default for Compose)
SPAWNER_TYPE=kubernetes  # production K8s mode        (default when unset)
```

The Dispatcher reads this at startup and registers the correct `IJobSpawner`.

---

## docker-compose.yml Changes

```yaml
dispatcher:
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock   # NEW: Docker socket access
    - ./config:/app/config
  environment:
    - SPAWNER_TYPE=docker                          # NEW: use DockerJobSpawner
    - REDIS_URL=redis:6379                         # unchanged, now works natively
    # K8S_REDIS_URL removed entirely
```

The `agentsmith` service stays in `docker-compose.yml` as an image definition.
It is never started directly — `DockerJobSpawner` references `agentsmith:latest`.

---

## Phase 19a Steps

| Step | File | Description |
|------|------|-------------|
| 19a-1 | `phase19a-interface.md` | Define IJobSpawner, rename existing class to KubernetesJobSpawner |
| 19a-2 | `phase19a-docker-spawner.md` | Implement DockerJobSpawner via Docker.DotNet |
| 19a-3 | `phase19a-di-wiring.md` | SPAWNER_TYPE-based DI registration in Dispatcher Program.cs |
| 19a-4 | `phase19a-compose.md` | Update docker-compose.yml, remove K8S_REDIS_URL from JobSpawner |

---

## Constraints & Notes

- Docker socket mount (`/var/run/docker.sock`) is intentional for local dev.
  It is **not** used in prod (K8s mode has no socket mount).
- Parallelism: Docker mode supports one job per channel.
  `ConversationStateManager` enforces this — same behaviour as today.
- The K8s path (`phase19b`) is completely unaffected by this phase.
- `apply-k8s-secret.sh` remains for K8s deployments but is no longer needed
  for local Docker Compose development.

---

## Success Criteria

- [ ] `docker compose up` brings up redis + dispatcher with no K8s involvement
- [ ] Slack message "fix #58 in agent-smith-test" spawns a Docker container (not a K8s Job)
- [ ] `docker ps` shows the ephemeral `agentsmith` container during execution
- [ ] Container disappears automatically after completion (AutoRemove=true)
- [ ] Progress messages reach Slack via Redis (`redis:6379`, same Compose network)
- [ ] `SPAWNER_TYPE=kubernetes` still routes to KubernetesJobSpawner correctly
- [ ] `host.docker.internal` does not appear anywhere in the codebase
- [ ] `K8S_REDIS_URL` does not appear anywhere in the codebase

---

## Dependencies

- `Docker.DotNet` NuGet package (MIT, official Docker SDK for .NET)
- Docker socket available at `/var/run/docker.sock` on host
- `agentsmith:latest` image built locally before running Compose

---

# Phase 19a-3: DI Wiring + Program.cs Refactoring

## Goal

Replace the monolithic `Program.cs` with a clean, extensible structure using
extension methods and dedicated handler classes. `Program.cs` becomes ~30 lines.

---

## What & Why

**Problem:** `Program.cs` was ~500 lines — a god class violating every coding
principle. All HTTP endpoints, intent handlers, DI wiring, and helper functions
were mixed together. Adding Teams support would have made it unmanageable.

**Solution:** Split into focused, single-responsibility classes:

| Class | Responsibility |
|---|---|
| `DispatcherDefaults` | All magic strings/values in one place |
| `DispatcherBanner` | ASCII banner on startup |
| `ServiceCollectionExtensions` | DI wiring as fluent extension methods |
| `WebApplicationExtensions` | Endpoint mapping (`MapSlackEndpoints`, etc.) |
| `SlackMessageDispatcher` | Parse intent → route to correct handler |
| `SlackInteractionHandler` | Handle button callbacks (yes/no answers) |
| `SlackSignatureVerifier` | HMAC-SHA256 request verification, isolated + testable |
| `FixTicketIntentHandler` | Handle fix intents, spawn job, register state |
| `ListTicketsIntentHandler` | Query ticket provider, format and send list |
| `CreateTicketIntentHandler` | Create ticket, send confirmation |

**Convention over Configuration:** All defaults live in `DispatcherDefaults`.
No magic strings scattered across the codebase.

---

## Structure

```
AgentSmith.Dispatcher/
├── Program.cs                          ← ~30 lines
├── DispatcherBanner.cs
├── DispatcherDefaults.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  ← AddRedis, AddCore, AddSlack, AddJobSpawner, AddIntentHandlers
│   └── WebApplicationExtensions.cs     ← MapHealthEndpoints, MapSlackEndpoints
├── Handlers/
│   ├── FixTicketIntentHandler.cs
│   ├── ListTicketsIntentHandler.cs
│   └── CreateTicketIntentHandler.cs
└── Adapters/
    ├── SlackMessageDispatcher.cs
    ├── SlackInteractionHandler.cs
    └── SlackSignatureVerifier.cs
```

---

## Key Design Decisions

**`AddJobSpawnerAsync` is async** — it performs a connectivity check (Docker ping
or K8s API call) at startup to give immediate feedback in logs. If the spawner
is unavailable, `IJobSpawner` is simply not registered. The `FixTicketIntentHandler`
handles the `null` case gracefully.

**Intent handlers are `Scoped`** — each Slack message creates a new scope,
so handlers get fresh dependencies per request.

**`SlackSignatureVerifier` takes `signingSecret` in constructor** — stateless,
no DI needed, fully testable without mocks.

**Teams-ready:** Adding Teams support means:
- `MapTeamsEndpoints()` in `WebApplicationExtensions`
- `TeamsMessageDispatcher` + `TeamsInteractionHandler` in `Adapters/`
- `Program.cs` unchanged

---

## Adding a New Platform (Teams example)

```csharp
// Program.cs — one line added:
app.MapHealthEndpoints()
   .MapSlackEndpoints()
   .MapTeamsEndpoints();  // ← add this
```

Everything else is self-contained in the Teams adapter classes.

---

## Files

| File | Change |
|------|--------|
| `Program.cs` | REWRITTEN — ~30 lines, only wiring + app.Run() |
| `DispatcherDefaults.cs` | NEW — all constants |
| `DispatcherBanner.cs` | NEW — extracted from Program.cs |
| `Extensions/ServiceCollectionExtensions.cs` | NEW — AddRedis, AddCore, AddSlack, AddJobSpawner, AddIntentHandlers |
| `Extensions/WebApplicationExtensions.cs` | NEW — MapHealthEndpoints, MapSlackEndpoints |
| `Handlers/FixTicketIntentHandler.cs` | NEW — extracted from Program.cs |
| `Handlers/ListTicketsIntentHandler.cs` | NEW — extracted from Program.cs |
| `Handlers/CreateTicketIntentHandler.cs` | NEW — extracted from Program.cs |
| `Adapters/SlackMessageDispatcher.cs` | NEW — intent routing |
| `Adapters/SlackInteractionHandler.cs` | NEW — button callback handling |
| `Adapters/SlackSignatureVerifier.cs` | NEW — HMAC verification |

---

## Success Criteria

- [ ] `Program.cs` is ≤ 30 lines
- [ ] No class exceeds 120 lines
- [ ] No method exceeds 20 lines
- [ ] No magic strings in any class (all in `DispatcherDefaults`)
- [ ] No `Console.WriteLine` — only `ILogger`
- [ ] `dotnet build` succeeds
- [ ] Slack fix/list/create still works after refactoring
- [ ] Adding a second platform requires zero changes to `Program.cs`


---

# Phase 19a-2: DockerJobSpawner

## Goal

Implement `DockerJobSpawner` — an `IJobSpawner` that spawns ephemeral agent
containers via the Docker Engine API instead of the Kubernetes API.
Selected when `SPAWNER_TYPE=docker`.

---

## What & Why

**Problem:** In local Docker Compose mode, spawning K8s Jobs required the
`host.docker.internal:6379` Redis hack because the job ran outside the
Compose network.

**Solution:** Use the Docker socket to start the agent as a regular container
inside the same Compose network. Redis is reachable at `redis:6379` natively —
no hacks, no K8s required.

**Secrets:** Forwarded directly from the Dispatcher's own environment variables.
No K8s Secret or external secret manager needed for local dev.

**Cleanup:** `AutoRemove=true` — the container removes itself after the agent
process exits. Replaces the K8s `TtlSecondsAfterFinished`.

---

## Network Resolution

Priority order to determine which Docker network the agent container joins:

1. `DOCKER_NETWORK` env var (explicit override)
2. Auto-detect: inspect the Dispatcher's own container (hostname = container ID in Docker)
   and read its first network
3. Fallback: `bridge`

In Docker Compose, option 2 reliably picks up the Compose project network
(e.g. `agent-smith_default`).

---

## Docker Socket Access

On macOS, `/var/run/docker.sock` is owned by `root:root` with `srwxr-xr-x`.
Non-root users cannot write to it. The Dispatcher container must run as `root`
in local dev mode.

In `docker-compose.yml`:
- Mount: `/var/run/docker.sock:/var/run/docker.sock`
- User override: `user: root`
- `SPAWNER_TYPE=docker`
- `K8S_REDIS_URL` removed entirely

This is intentional and acceptable for local development only.
In K8s prod mode, no socket is mounted and the Dispatcher runs as non-root.

---

## Resource Limits

Same as the K8s Job: 512Mi/1Gi memory, 250m/1000m CPU. Consistent behaviour
between local and prod environments.

---

## Files

| File | Change |
|------|--------|
| `AgentSmith.Dispatcher.csproj` | Add `Docker.DotNet` NuGet package |
| `Services/DockerJobSpawner.cs` | NEW — full implementation |
| `docker-compose.yml` | Socket mount, `user: root`, `SPAWNER_TYPE=docker`, remove `K8S_REDIS_URL` |

---

## Success Criteria

- [ ] `SPAWNER_TYPE=docker` selects `DockerJobSpawner` at startup
- [ ] Slack "fix #58" spawns a Docker container (visible in `docker ps`)
- [ ] Container name follows `agentsmith-{jobId}` pattern
- [ ] Container disappears after completion (`AutoRemove=true`)
- [ ] Agent reaches Redis at `redis:6379` (no `host.docker.internal`)
- [ ] Secrets reach the agent container via forwarded env vars
- [ ] Missing image produces a clear, actionable error message
- [ ] Network auto-detection works in Docker Compose

---

# Phase 19a-1: IJobSpawner Interface + KubernetesJobSpawner

## Goal

Extract a clean `IJobSpawner` interface from the existing `JobSpawner` class.
Rename `JobSpawner` to `KubernetesJobSpawner`. This enables `DockerJobSpawner`
to be added without touching any existing logic.

---

## What & Why

**Problem:** `JobSpawner` was tightly coupled to Kubernetes. No abstraction existed
to swap in a different spawner for local development.

**Solution:** Standard interface extraction. `IJobSpawner` has a single method:
`SpawnAsync(FixTicketIntent, CancellationToken) → Task<string>` returning the jobId.

**Side effect removed:** The `K8S_REDIS_URL` hack is eliminated. `KubernetesJobSpawner`
now uses `REDIS_URL` directly — in K8s mode, Redis runs in the same cluster.

---

## Files

| File | Change |
|------|--------|
| `Services/IJobSpawner.cs` | NEW — single-method interface |
| `Services/JobSpawner.cs` | RENAMED → `KubernetesJobSpawner.cs` |
| `Services/KubernetesJobSpawner.cs` | Class + logger renamed, implements `IJobSpawner`, `K8S_REDIS_URL` removed |
| `Services/KubernetesJobSpawner.cs` | `JobSpawnerOptions` extended with `DockerNetwork` property (used only by DockerJobSpawner) |

---

## Success Criteria

- [ ] `IJobSpawner` defined with single `SpawnAsync` method
- [ ] `KubernetesJobSpawner` implements `IJobSpawner`
- [ ] No references to `JobSpawner` remain in the codebase
- [ ] `K8S_REDIS_URL` does not appear anywhere in the codebase
- [ ] `dotnet build` succeeds