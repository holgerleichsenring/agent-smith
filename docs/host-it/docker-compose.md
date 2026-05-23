# Host it: docker-compose

The middle ground. One host, three containers — orchestrator, sandbox-agent, Redis — and your webhooks pointed at the orchestrator. Easiest path to a "real" Agent Smith deployment.

## What this gets you

- Long-running orchestrator. Webhooks work, polling works, jobs queue in Redis and survive restarts.
- Per-repo sandboxes. The orchestrator creates Docker containers on demand, one per repo per run, using the toolchain image you've configured.
- A persistent Redis. In-flight jobs, claim locks, leader leases, the run-id high-water mark — all there.

The orchestrator pod is single-replica in this setup. For multi-replica you want [Kubernetes](kubernetes.md).

## The compose file

`docker-compose.yml`:

```yaml
services:

  orchestrator:
    image: holgerleichsenring/agent-smith:0.60.1
    restart: unless-stopped
    ports:
      - "8080:8080"                                  # webhook endpoint
    environment:
      AGENTSMITH_CONFIG:    /etc/agent-smith/agentsmith.yml
      REDIS_URL:            redis://redis:6379
      AZURE_OPENAI_API_KEY: ${AZURE_OPENAI_API_KEY}
      AZURE_DEVOPS_TOKEN:   ${AZURE_DEVOPS_TOKEN}
      SANDBOX_TYPE:         docker
      DOCKER_HOST:          unix:///var/run/docker.sock
    volumes:
      - ./agentsmith.yml:/etc/agent-smith/agentsmith.yml:ro
      - ./runs:/var/lib/agent-smith/runs            # run directories
      - ./skills:/var/lib/agentsmith/skills         # skills cache
      - /var/run/docker.sock:/var/run/docker.sock   # orchestrator creates sandbox containers
    depends_on:
      redis:
        condition: service_healthy

  # Sandbox-agent image is referenced by the orchestrator when it creates
  # per-repo sandbox containers. We don't run it as a service — we just
  # make sure the image is pulled so the orchestrator can use it.
  sandbox-agent-pull:
    image: holgerleichsenring/agent-smith-sandbox-agent:0.60.1
    command: ["true"]
    restart: "no"

  redis:
    image: redis:7
    restart: unless-stopped
    command: ["redis-server", "--appendonly", "yes"]
    volumes:
      - ./redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5
```

Drop your `agentsmith.yml` (full TodoList example on the [Azure DevOps tracker page](../connect-your-stuff/tracker-azure-devops.md)) next to this compose file, set the env vars, bring it up:

```bash
export AZURE_OPENAI_API_KEY=...
export AZURE_DEVOPS_TOKEN=...

docker compose pull
docker compose up -d

# Watch the orchestrator come up
docker compose logs -f orchestrator
```

## Webhooks

The orchestrator listens on port 8080. For a public webhook URL you need to put a reverse proxy in front (or Cloudflare Tunnel / ngrok for dev). The simplest production setup is Caddy:

`Caddyfile`:

```
agent-smith.your-domain.example {
  reverse_proxy orchestrator:8080
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

Caddy gets you automatic Let's Encrypt TLS. Point your DNS at the host, point the tracker webhooks at `https://agent-smith.your-domain.example/webhooks/{tracker-type}`, done.

## Storage

Three volumes the orchestrator writes to:

- `./runs/` — run directories accumulate here. Manageable size (a few MB per run). Keep them; the wiki-compile feature uses the history.
- `./skills/` — the skills cache. Pulled from the `agent-smith-skills` repo at startup; rebuilt when `skills.version` changes.
- `./redis-data/` — Redis append-only log. Small unless you have very long runs.

For a real production setup put these on a persistent volume (or named Docker volume) so a host rebuild doesn't lose state.

## Logs

```bash
docker compose logs -f orchestrator   # live
docker compose logs --tail 100 orchestrator
```

Orchestrator logs are structured (JSON if `LOG_FORMAT=json` is set, otherwise human-readable). Per-run logs also land in the run directory's `result.md`.

## Updating

```bash
# Update the version pin in both places
sed -i 's/0.60.1/0.60.2/g' docker-compose.yml
sed -i 's/0.60.1/0.60.2/g' agentsmith.yml   # the `sandbox.agent_version` + `orchestrator.version`

docker compose pull
docker compose up -d
```

The two version numbers (image tag + `agentsmith.yml` sandbox/orchestrator versions) must match — that's the upgrade contract. Skills version (`skills.version`) is independent; bump it whenever you want.

## What this isn't

- It's not HA. One orchestrator, one Redis. If the orchestrator dies in the middle of a run, the run gets restarted from the queue on the next startup (the `StaleJobDetector` notices and re-enqueues).
- It's not auto-scaling. For more concurrent runs, more replicas, you want Kubernetes.

## Next

- [Kubernetes](kubernetes.md) — when one orchestrator isn't enough.
- [Webhooks](../trigger-it/webhooks.md) — wiring the tracker to your new public URL.
- [First run](../get-it-running/first-run.md) — but from the host this time, via the tracker.
