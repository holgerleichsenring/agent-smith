# Host it: docker-compose

The middle ground. One host, a handful of containers — the server, Redis, a one-shot migrate job, optionally the dashboard — and your webhooks pointed at the server. Easiest path to a "real" Agent Smith deployment. The reference compose file ships in the repo under `deploy/docker-compose.yml`.

## What this gets you

- Long-running server. Webhooks work, polling works, chat (Slack / Teams) works, jobs queue and survive restarts. One process does all of it (`AgentSmith.Server`).
- A relational system of record. Every run fact lands in a database (SQLite on a volume by default; point `persistence:` at Postgres / MySQL for a shared setup). Redis carries the in-flight queue and change notifications.
- Per-repo sandboxes. The server creates Docker containers on demand, one per repo per run, using the toolchain image your repos need. The sandbox-agent image is injected into them as a carrier.
- The dashboard on port 3000, if you enable its profile — runs list, live timeline, system view, config explorer, connection diagnostics.

The server is single-replica in this setup. For multi-replica you want [Kubernetes](kubernetes.md).

## The pieces

`deploy/docker-compose.yml` defines:

| Service | Image | Role |
|---|---|---|
| `server` | `holgerleichsenring/agent-smith-server` | The long-running process: webhooks (port 8081), polling, queue consumer, chat, reconcilers. |
| `migrate` | `holgerleichsenring/agent-smith-cli` | One-shot `agentsmith database migrate` — applies schema migrations, then exits. The server waits for it. |
| `redis` | `redis:7-alpine` | In-flight queue, change notifications, leases. AOF persistence on a volume. |
| `dashboard` | `holgerleichsenring/agentsmith-dashboard` | Optional (compose profile `dashboard`), port 3000, proxies to the server. |
| `sandbox-agent` | `holgerleichsenring/agent-smith-sandbox-agent` | Not a service — the carrier image the spawner injects into per-repo sandbox containers. Just needs to be present. |
| `agentsmith` | `holgerleichsenring/agent-smith-cli` | One-shot CLI for ad-hoc runs against the same config. |

The env vars that matter on the server:

```bash
REDIS_URL=redis:6379          # host:port — no scheme prefix
SPAWNER_TYPE=docker           # spawn runs as Docker containers on this host
SERVER_PORT=8081              # published webhook/API port
AGENTSMITH_VERSION=0.108.0    # image tag pin for all agent-smith images
```

Plus your secrets (`ANTHROPIC_API_KEY` / `OPENAI_API_KEY`, tracker tokens, `GITHUB_WEBHOOK_SECRET` / `GITLAB_WEBHOOK_TOKEN` / `AZDO_WEBHOOK_SECRET`) in an `.env` file next to the compose file.

Your `agentsmith.yml` is bind-mounted at `/app/config/agentsmith.yml`. Bring it up:

```bash
docker compose -f deploy/docker-compose.yml up -d
docker compose -f deploy/docker-compose.yml --profile dashboard up -d   # with the dashboard

# Watch the server come up
docker compose -f deploy/docker-compose.yml logs -f server
```

`GET http://localhost:8081/health` tells you how the subsystems are doing — including the startup preflight report (the same checks `agent-smith doctor` runs, warn-only on the server so a degraded tracker doesn't become an outage).

## Webhooks

The server listens on port 8081; the webhook endpoint is `POST /webhook` (see [Trigger: webhooks](../trigger-it/webhooks.md)). For a public webhook URL you need a reverse proxy in front (or Cloudflare Tunnel / ngrok for dev). The simplest production setup is Caddy:

`Caddyfile`:

```
agent-smith.your-domain.example {
  reverse_proxy server:8081
}
```

```yaml
# add to docker-compose.yml
  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
```

Caddy gets you automatic Let's Encrypt TLS. Point your DNS at the host, point the tracker webhooks at `https://agent-smith.your-domain.example/webhook`, done.

## Storage

Three volumes the deployment writes to:

- The database volume (`/var/lib/agentsmith`) — the SQLite system of record, shared between `migrate` and `server`. This is the one you really don't want to lose.
- The Redis volume — append-only log for in-flight state. Small.
- The config bind-mount (`/app/config`) — read by the server, the migrate job, and ad-hoc CLI runs.

Run directories land in the repos themselves (committed under `.agentsmith/runs/` on the run branch), so the why-record travels with the code.

## Logs

```bash
docker compose -f deploy/docker-compose.yml logs -f server
docker compose -f deploy/docker-compose.yml logs --tail 100 server
```

For per-run visibility, the [dashboard](../reference/operations/dashboard.md) beats raw logs: live step timeline, per-call LLM cost, sandbox stdout, cancel button.

## Updating

```bash
# .env
AGENTSMITH_VERSION=0.108.0
```

```yaml
# agentsmith.yml — the same number
deployment:
  registry: holgerleichsenring
  version: 0.108.0
```

```bash
docker compose -f deploy/docker-compose.yml pull
docker compose -f deploy/docker-compose.yml up -d
```

The image tag and the `deployment:` pin must match — that's the upgrade contract, one number in two places. The migrate job re-runs on every `up` and applies whatever migrations the new version brought. Skills upgrade with the image: each release embeds the catalog it was tested with, so there is no separate skills pin to bump.

## Capacity

One host means finite capacity. `queue.MaxParallelJobs` (default 4) bounds concurrent runs, and the Docker capacity probe (`max_concurrent_sandboxes`) queues a run instead of overcommitting the host — queued runs show up amber in the dashboard with their position. Details on the [capacity page](../reference/operations/capacity.md).

## What this isn't

- It's not HA. One server, one Redis, one SQLite. If the server dies mid-run, the run gets reconciled on the next startup — the DB knows what was in flight.
- It's not auto-scaling. For more concurrent runs and real quotas, you want [Kubernetes](kubernetes.md).

## Next

- [Kubernetes](kubernetes.md) — when one server isn't enough.
- [Webhooks](../trigger-it/webhooks.md) — wiring the tracker to your new public URL.
- [Dashboard](../reference/operations/dashboard.md) — watch the runs you just enabled.
