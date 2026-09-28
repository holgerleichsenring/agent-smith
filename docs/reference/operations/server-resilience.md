# Server resilience

The server (`AgentSmith.Server.dll`, the one long-running deployment) always starts. A missing database, an unreachable Redis, a broken configuration or no reachable container backend don't crash the container. Each one becomes a **finding**: the server comes up, serves what still works, and tells you what doesn't and why. A server that crash-loops can't explain itself, and the explanation is the thing you need.

## Startup findings

Before the listener binds, the server asks each startup dependency whether it's there:

| Subsystem | Asks |
|---|---|
| `config-file` | Is the bootstrap file there? Without it the server falls back to built-in defaults, which is fine for a local run and serious in a container. |
| `configuration` | Does the configuration load and validate? |
| `database` | Is the database reachable? |
| `redis` | Is Redis reachable? Without it the job queue, leader election and the live run feed are down, so no run can be queued or picked up. |
| `spawner` | Is a real job spawner (Docker or Kubernetes) behind the one that was registered? |
| `sandbox-agent` | Is a project's sandbox agent pinned to a different version than this server's release? Advisory only. |

Each probe gets ten seconds. A probe that doesn't answer in time, or throws, is recorded as a blocking finding that says its state is unknown. Other parts of the server add findings as they run into things, for example an `auth` finding when the [token authority is unreachable](../security/access-control.md#when-the-authority-is-unreachable) or a `build` finding when a dashboard tab and the server come from different builds.

A finding names the subsystem, a severity (`blocking` or `advisory`) and a reason, and where it applies, the project, trigger and field. Read them at `GET /api/config/findings`. The route answers without a token, because the channel that reports a broken authority can't depend on that authority:

```json
{
  "degraded": true,
  "blocking": 1,
  "advisory": 0,
  "findings": [
    {
      "subsystem": "redis",
      "severity": "blocking",
      "reason": "Redis is not reachable, so the job queue, leader election and the live run feed are down — no run can be queued or picked up. Cause: …",
      "project": null,
      "trigger": null,
      "field": "REDIS_URL"
    }
  ]
}
```

The server also logs one line at startup with the count and a pointer to this route. With any blocking finding, the dashboard shows an amber banner over every page, "Running degraded — 1 blocking finding. Everything not named below still runs.", followed by the findings. `agent-smith config validate` prints the same findings for a configuration file without starting a server.

## `GET /health`

Liveness. It answers `200` whenever the listener is alive, needs no token, and carries the startup preflight's verdict so a `curl` shows what to fix without reading logs:

```json
{
  "status": "ok",
  "timestamp": "2026-09-28T09:12:44Z",
  "preflight": {
    "status": "fail",
    "completed_at_utc": "2026-09-28T09:10:02.118Z",
    "passed": 11,
    "failed": 1,
    "skipped": 2,
    "failures": [
      { "name": "sign-in", "message": "…", "fix_hint": "…" }
    ]
  }
}
```

`preflight.status` is `pending` until the startup run finished, then `pass` or `fail`. The checks are the same ones `agent-smith doctor` runs, plus the server-only ones such as `sign-in`. Point liveness probes here: Kubernetes and Docker should not restart the pod because Redis is briefly gone. The shipped manifests do exactly that (`deploy/k8s/8-deployment-server.yaml`, the compose healthcheck).

There is no separate readiness endpoint. For alerting, watch `degraded` on `/api/config/findings` and `preflight.status` on `/health`.

## The Redis-backed subsystems

Three background subsystems need Redis: `queue_consumer` (pulls queued runs and executes them), `housekeeping` (stale-job detection and reconciliation, leader-elected) and `poller` (ticket polling per tracker, leader-elected). Webhook routes, the dashboard API and the config studio don't; they're served by the same listener regardless.

Each of the three is in one of four states:

- **Up**: running normally.
- **Degraded**: Redis is configured but not connected, or the task crashed and is retrying.
- **Down**: fatal error.
- **Disabled**: `REDIS_URL` is not set. The subsystem won't start in this process; restart with `REDIS_URL` set.

The Redis connection is built not to abort on a failed connect, so it reconnects on its own once Redis is reachable. While degraded, each subsystem checks every `queue.redis_retry_interval_seconds` (default 30) and starts its work as soon as the connection is up. Each state change writes one log line, which keeps the log readable during an outage.

## Webhooks while Redis is down

A webhook that carries an answer to a parked run's question needs Redis to route it. While Redis is not up, that delivery gets:

```
HTTP/1.1 503 Service Unavailable

redis_unavailable
```

and the trigger log records it as skipped with `redis-unavailable`. GitHub, GitLab, Azure DevOps and Jira retry deliveries on a 503, so the answer arrives once Redis is back.

## Next

- [Dashboard](dashboard.md): the degraded and build banners, and the Connection check.
- [Access control](../security/access-control.md): the `sign-in` preflight check and an unreachable authority.
- [Metrics](metrics.md): the counters the server exposes.
