# Server Resilience

The CLI server (`agent-smith server`) is split into four subsystems with independent
health states. Three of them depend on Redis; the webhook listener does not. The
server boots even when Redis is missing or unreachable — it stays up and reports
*why* it is degraded via the `/health` endpoints, instead of crashing the container.

## Subsystems

| Subsystem        | Needs Redis | Purpose                                                |
|------------------|-------------|--------------------------------------------------------|
| `webhook`        | No          | HTTP listener accepting webhook POSTs and `/health`.   |
| `redis`          | Yes         | StackExchange.Redis multiplexer connection state.      |
| `queue_consumer` | Yes         | Pulls `PipelineRequest`s off the queue and runs them.  |
| `housekeeping`   | Yes         | Stale-job detector + enqueued reconciler (leader-elected). |
| `poller`         | Yes         | Per-platform ticket polling (leader-elected).          |

Each subsystem is in one of four states:

- **Up** — running normally.
- **Degraded** — temporarily impaired (Redis is configured but disconnected;
  task crashed and is retrying).
- **Down** — listener stopped or fatal error.
- **Disabled** — `REDIS_URL` is not configured. The subsystem will never start
  in this process; restart with `REDIS_URL` set to enable it.

## Endpoints

### `GET /health` — liveness

Always returns HTTP 200 as long as the listener is alive. The body is JSON
describing every subsystem:

```json
{
  "status": "degraded",
  "subsystems": [
    { "name": "webhook",        "state": "up",       "reason": null,                          "last_changed_utc": "2026-04-26T12:00:01Z" },
    { "name": "queue_consumer", "state": "disabled", "reason": "REDIS_URL not configured",    "last_changed_utc": "2026-04-26T12:00:00Z" },
    { "name": "housekeeping",   "state": "disabled", "reason": "REDIS_URL not configured",    "last_changed_utc": "2026-04-26T12:00:00Z" },
    { "name": "poller",         "state": "disabled", "reason": "REDIS_URL not configured",    "last_changed_utc": "2026-04-26T12:00:00Z" },
    { "name": "redis",          "state": "disabled", "reason": "REDIS_URL not configured",    "last_changed_utc": "2026-04-26T12:00:00Z" }
  ]
}
```

`status` is `ok` when every subsystem is `up`, otherwise `degraded`. Use
`/health` for container liveness probes — Kubernetes / Docker should not restart
the pod just because Redis is briefly down.

### `GET /health/ready` — readiness (loud-fail)

Returns HTTP 503 whenever **any** subsystem is not `Up` — including `Disabled`.
This is intentional: a server with `REDIS_URL` unset is technically alive but
silently rejecting every webhook with 503, which would otherwise look identical
to a healthy server in monitoring. Loud-fail readiness ensures operators see
the misconfiguration immediately:

- 200 + `{"status": "ready"}` — every subsystem is `Up`.
- 503 + `{"status": "not_ready", "subsystems": [...]}` — at least one subsystem
  is not `Up`. The body lists every subsystem with its current state and
  reason so the operator can see *why* at a glance.

Use `/health/ready` for ingress / load-balancer readiness gates and alerting.

## Behaviour by deployment configuration

| Configuration                          | `webhook` | `redis`    | `queue_consumer` | `/health` | `/health/ready` |
|----------------------------------------|-----------|------------|------------------|-----------|-----------------|
| `REDIS_URL` set, Redis reachable       | Up        | Up         | Up               | 200 ok    | 200 ready       |
| `REDIS_URL` set, Redis unreachable     | Up        | Degraded   | Degraded         | 200 degraded | 503 not_ready |
| `REDIS_URL` unset                      | Up        | Disabled   | Disabled         | 200 degraded | 503 not_ready |
| Listener stopped (graceful shutdown)   | Down      | (any)      | (any)            | 200 / shutdown | 503        |

## Recovery semantics

- `IConnectionMultiplexer` is built with `AbortOnConnectFail=false`, so it
  reconnects automatically when Redis becomes reachable.
- `queue_consumer`, `housekeeping`, and `poller` each poll the multiplexer
  every `queue.redis_retry_interval_seconds` (default 30s, configurable in
  `agentsmith.yml`) while in `Degraded`. When the multiplexer reports
  `IsConnected=true` they transition to `Up` and start their work.
- A single `INFO` log line per state transition keeps the log readable during
  outages.

## Webhook behaviour while Redis is down

Structured ticket webhooks (`/webhook/jira`, `/webhook/github`, …) need
`ITicketClaimService` to enqueue work. When Redis is unavailable, every
structured webhook responds:

```
HTTP/1.1 503 Service Unavailable

redis_unavailable
```

Dialogue-answer webhooks (PR comment paths) reply 503 with the same body for
the same reason. Free-form `TriggerInput` webhooks that run pipelines
in-process (no claim required) continue to work.

GitHub / GitLab / Azure DevOps / Jira retry their webhook deliveries on 503,
so once Redis is restored, queued events are replayed to the server.
