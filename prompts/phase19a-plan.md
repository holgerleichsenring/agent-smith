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