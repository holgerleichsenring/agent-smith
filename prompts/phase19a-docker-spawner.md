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