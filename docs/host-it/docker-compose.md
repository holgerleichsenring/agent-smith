# Host it: docker-compose

The middle ground. One host, a handful of containers — the server, Redis, a one-shot migrate job, optionally the dashboard — and your webhooks pointed at the server. Easiest path to a "real" Agent Smith deployment. The reference compose file ships in the repo as `deploy/docker-compose.example.yml`. Copy it to `deploy/docker-compose.yml` before you start, since that is the name every command below uses:

```bash
cp deploy/docker-compose.example.yml deploy/docker-compose.yml
```

## What this gets you

- Long-running server. Webhooks work, polling works, chat (Slack / Teams) works, jobs queue and survive restarts. One process does all of it (`AgentSmith.Server`).
- A relational system of record. Every run fact lands in a database (SQLite on a volume by default; point `persistence:` at PostgreSQL, MySQL or SQL Server for a shared setup). Redis carries the in-flight queue and change notifications.
- Per-repo sandboxes. The server creates Docker containers on demand, one per repo and toolchain image per run. The sandbox-agent image is injected into them as a carrier, and the package caches outlive them (see [Package cache](#package-cache)).
- The dashboard on port 3000, if you enable its profile — runs list, live timeline, system view, config explorer, connection diagnostics.

The server is single-replica in this setup. For multi-replica you want [Kubernetes](kubernetes.md).

## The pieces

It defines seven services:

| Service | Image | Role |
|---|---|---|
| `server` | `holgerleichsenring/agent-smith-server` | The long-running process: webhooks (port 8081), polling, queue consumer, chat, reconcilers. |
| `migrate` | `holgerleichsenring/agent-smith-cli` | One-shot `agentsmith database migrate` — applies schema migrations, then exits. The server waits for it. |
| `redis` | `redis:7-alpine` | In-flight queue, change notifications, leases. AOF persistence on a volume. |
| `dashboard` | `holgerleichsenring/agentsmith-dashboard` | Optional (compose profile `dashboard`), port 3000, proxies to the server. |
| `sandbox-agent` | `holgerleichsenring/agent-smith-sandbox-agent` | Not a service — the carrier image the spawner injects into per-repo sandbox containers. Just needs to be present. |
| `agentsmith` | `holgerleichsenring/agent-smith-cli` | One-shot CLI for ad-hoc runs against the same config. |
| `ollama` | `ollama/ollama` | Optional local model server, for running against Ollama instead of a hosted provider. |

The env vars that matter on the server:

```bash
REDIS_URL=redis:6379          # host:port — no scheme prefix
SPAWNER_TYPE=docker           # spawn runs as Docker containers on this host
SERVER_PORT=8081              # published webhook/API port
AGENTSMITH_VERSION=0.108.0    # image tag pin for all agent-smith images
```

Plus your secrets (`ANTHROPIC_API_KEY` / `OPENAI_API_KEY`, tracker tokens, `GITHUB_WEBHOOK_SECRET` / `GITLAB_WEBHOOK_TOKEN` / `AZDO_WEBHOOK_SECRET`) in an `.env` file next to the compose file.

Your `agentsmith.yml` is bind-mounted at `/app/config/agentsmith.yml`, and for a server that file is the bootstrap slice. `persistence:` and `secrets:`, nothing more:

```yaml
persistence:
  provider: sqlite            # sqlite | postgresql | mysql | sqlserver
  connection_string: "Data Source=/var/lib/agentsmith/agentsmith.db"

secrets:
  github_token: ${GITHUB_TOKEN}
```

The provider keys don't need a `secrets:` entry; every agent reads its key straight from the environment (see [AI providers](../connect-your-stuff/ai-providers.md#api-keys)).

A `${…}` placeholder inside `connection_string` is not expanded; this block is read before any secret exists. To keep a database password out of the file, set `AGENTSMITH_PERSISTENCE_PROVIDER` and `AGENTSMITH_PERSISTENCE_CONNECTION` in `.env` instead. Both `migrate` and `server` read that file, and the pair replaces the `persistence:` block. Setting only one of the two is refused with a startup finding.

The catalog (agents, trackers, repos, projects) lives in the database and gets edited in the dashboard. A big `agentsmith.yml` mounted here is not an error, it is just ignored past those two blocks, which is a confusing way to spend an afternoon. See [Where configuration lives](../configure-it/index.md).

Bring it up:

```bash
docker compose -f deploy/docker-compose.yml up -d
docker compose -f deploy/docker-compose.yml --profile dashboard up -d   # with the dashboard

# Watch the server come up
docker compose -f deploy/docker-compose.yml logs -f server
```

## First configuration

The migrate job creates the schema. It does not seed configuration, so a fresh stack comes up with an empty catalog on purpose, and nothing gets invented behind your back.

Two ways to fill it:

```bash
# you already have a working config file (from a CLI setup, or another environment)
docker compose -f deploy/docker-compose.yml run --rm agentsmith config import /app/config/agentsmith-full.yml
```

or bring up the dashboard profile, open `http://localhost:3000`, switch the rail to **Configuration**, and build the catalog there. Either way the result lands in the database and the running server picks it up without a restart.

To get it back out again, for backups, code review, or seeding a second environment:

```bash
docker compose -f deploy/docker-compose.yml run --rm agentsmith config export --output /app/config/agentsmith-backup.yml
```

## Health

`GET http://localhost:8081/health` tells you how the subsystems are doing: each background subsystem (queue consumer, housekeeping, poller, capacity queue, Redis) with its state and the reason it is not up, plus the startup preflight report (the same checks `agent-smith doctor` runs, warn-only on the server so a degraded tracker doesn't become an outage). It answers `200` either way; see [Server resilience](../reference/operations/server-resilience.md#get-health).

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

```bash
docker compose -f deploy/docker-compose.yml pull
docker compose -f deploy/docker-compose.yml up -d
```

That one number is the upgrade. The sandbox-agent tag is derived from the release the running server is, so the server and the agent it injects into sandboxes cannot drift apart by being forgotten. You only name a tag in `agentsmith.yml` when you want a different one on purpose, for example an air-gapped mirror that carries a single release:

```yaml
# agentsmith.yml, optional
deployment:
  registry: my-mirror.example/agent-smith
  version: 0.108.0       # fills sandbox.agent_version and the orchestrator tag where unset
```

A pinned tag is reported as an advisory finding and never refused. The installation page in the dashboard shows, per project, which agent tag is in use and whether it was derived or pinned.

The migrate job re-runs on every `up` and applies whatever migrations the new version brought. Skills upgrade with the image: each release embeds the catalog it was tested with, so there is no separate skills pin to bump unless you want one (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Building the images yourself

`docker compose -f deploy/docker-compose.yml build` builds the CLI, `migrate` and `server`; `--profile build-only build sandbox-agent` and `--profile dashboard build dashboard` build the rest. Nothing inside a build can read the git revision, so pass the identity in front of it:

```bash
AGENTSMITH_BUILD_REVISION=$(git rev-parse HEAD) \
AGENTSMITH_RELEASE_VERSION=$(cat version.txt) \
docker compose -f deploy/docker-compose.yml build
```

The installation page then reports that revision and version. Without the two variables the image is honestly unstamped and the page says "not stated by this build". A server that carries no release version has nothing to derive the sandbox-agent tag from, so on such a build set `deployment.version` (or `sandbox.agent_version`) to a published tag, or the first sandbox fails with a message that says exactly that.

To run a [Copilot agent](../connect-your-stuff/ai-providers.md#github-copilot), build the server image with `--build-arg COPILOT_CLI_VERSION=<version>`.

## Package cache

Every Docker sandbox mounts persistent package caches under `/pkgcache` (NuGet, npm, pip, Go, Cargo), and the matching environment variables (`NUGET_PACKAGES`, `NPM_CONFIG_CACHE`, `PIP_CACHE_DIR`, `GOMODCACHE`, `CARGO_HOME` and friends) point the tooling there. The volumes are named `agentsmith-pkgcache-<ecosystem>`, carry no run identity, and survive every sandbox, so the second restore of a solution reads from disk instead of the network.

It is on by default. `SANDBOX_PACKAGE_CACHE=false` on the server turns it off, for example to prove a cold restore. `docker volume rm agentsmith-pkgcache-npm` clears one ecosystem.

## Two deployments on one host

Sandbox containers are labelled with the deployment that owns them, and the orphan reaper and the capacity count only ever look at their own. The owner identity is derived from the Redis endpoint the server hands its sandboxes: two servers that share a Redis are one owner and clean up after each other, two that don't never see each other's containers. Set `SANDBOX_OWNER_ID` to name the owner yourself.

The reaper runs only where the server has a database-backed lease that can tell live runs from dead ones. Where it can't judge, it stands down with a log line saying why. `SANDBOX_ORPHAN_REAPER=true` or `false` forces the decision either way.

## Capacity

One host means finite capacity. `queue.MaxParallelJobs` (default 4) bounds concurrent runs, and the Docker capacity probe (`max_concurrent_sandboxes`) queues a run instead of overcommitting the host — queued runs show up amber in the dashboard with their position.

`max_concurrent_sandboxes` lives in the `sandbox:` settings (`maxConcurrentSandboxes` on the wire and in the dashboard, `MaxConcurrentSandboxes` in the stored document and the C# model) and is read at the moment the probe decides, so a change applies without restarting the server. Leave it empty and the `SANDBOX_MAX_CONCURRENT` environment variable is read instead — the fallback for an installation whose configuration store is still empty — and then the built-in default of 2. `0` means unbounded. Details on the [capacity page](../reference/operations/capacity.md#where-the-concurrent-sandbox-bound-is-set).

A design conversation in the dashboard keeps its read-only sandboxes between turns for `sandbox.hold_seconds` (default 180, see [Sandbox architecture](../reference/concepts/sandbox-architecture.md#read-only-source-sandboxes)). A hold is released before any capacity check, so a held sandbox never costs a run its slot.

## What this isn't

- It's not HA. One server, one Redis, one SQLite. If the server dies mid-run, the run gets reconciled on the next startup — the DB knows what was in flight.
- It's not auto-scaling. For more concurrent runs and real quotas, you want [Kubernetes](kubernetes.md).

## Next

- [Kubernetes](kubernetes.md) — when one server isn't enough.
- [Webhooks](../trigger-it/webhooks.md) — wiring the tracker to your new public URL.
- [Dashboard](../reference/operations/dashboard.md) — watch the runs you just enabled.
