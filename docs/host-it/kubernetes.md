# Host it: Kubernetes

For shared use and production. The server as a Deployment, Redis alongside, sandbox pods created on demand and disposed when each run ends. No CRDs, no operators — stock Kubernetes objects. The manifests ship in the repo under `deploy/k8s/`, numbered in apply order:

```
deploy/k8s/
├── 1-namespace.yaml
├── 2-rbac.yaml                  # ServiceAccount + Role + RoleBinding (pod create/delete)
├── 3-configmap.yaml             # your agentsmith.yml
├── 4-secret-template.yaml       # keys, tokens, REDIS_URL — fill in, never commit real values
├── 5-service-redis.yaml
├── 6-deployment-redis.yaml
├── 7-service-server.yaml
├── 8-deployment-server.yaml     # init-container migrate + the server
├── 9-ingress.yaml
├── 10-deployment-dashboard.yaml
└── 11-service-dashboard.yaml
```

```bash
kubectl apply -f deploy/k8s/
kubectl -n agentsmith rollout status deployment/agentsmith-server
```

## What this gets you

- The server survives node failures because Kubernetes reschedules it, and the run history survives everything because it lives in a relational database (SQLite on a PVC by default; point `persistence:` at Postgres / MySQL for anything shared — the same init-container migrates either).
- Sandbox pods created per repo per run, then deleted. The server's `ServiceAccount` has permission to create / delete pods in its own namespace — that's the whole RBAC story.
- Real capacity control: a `ResourceQuota` on the namespace turns "too many runs" into a FIFO queue instead of failures (see the capacity section below).
- The dashboard as its own Deployment + Service, port 3000 behind the Service.

## The shape of the server pod

The load-bearing details from `8-deployment-server.yaml`, so you know what you're looking at:

- **An init-container runs `agentsmith database migrate --config /app/config/agentsmith.yml`** before the server starts. Migrations are applied exactly there — the server never migrates its own database on startup, deliberately. It shares the persistence volume with the server (and for an external DB it migrates over the connection string instead).
- The server container listens on **8081** (`/health` liveness, `/health/ready` readiness). The startup preflight — the same checks as `agent-smith doctor` — runs warn-only in the background and reports on `/health`; a degraded tracker shows up there instead of blocking startup.
- Config is a ConfigMap mounted at **`/app/config/agentsmith.yml`**; secrets come from the `agentsmith-secrets` Secret (`REDIS_URL`, provider keys, tracker tokens, webhook secrets, optional Slack/Teams tokens).
- `SPAWNER_TYPE` is `kubernetes` by default in-cluster: each triggered run is spawned as its own short-lived orchestrator pod (the CLI image), which in turn creates the per-repo sandbox pods. That's why the quota math below counts "orchestrator + one sandbox per repo" per run.
- The images are one release: `holgerleichsenring/agent-smith-server`, `holgerleichsenring/agent-smith-cli`, `holgerleichsenring/agent-smith-sandbox-agent`, `holgerleichsenring/agentsmith-dashboard` — pin the same tag everywhere, and put the same number into the `deployment:` block of `agentsmith.yml`:

```yaml
deployment:
  registry: holgerleichsenring
  version: 0.108.0
```

- Skills need no pin: every release embeds the catalog it was tested with. The `skills` volume is an `emptyDir` the embedded catalog materializes into at startup.

## Webhooks

The Ingress (`9-ingress.yaml`) routes your public hostname to the server Service. Point your tracker webhooks at `https://agent-smith.your-domain.example/webhook` — endpoint details and per-tracker setup on the [webhooks page](../trigger-it/webhooks.md). The webhook shared secrets (`GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_TOKEN`, `AZDO_WEBHOOK_SECRET`) go into the Secret next to the API keys.

## Sandbox pods

The server creates a pod per repo per run. Each pod has an init-container that copies the sandbox-agent binary into a shared `emptyDir`, then the main toolchain container starts and the agent binary takes over the entrypoint. The toolchain image comes from the repo's stack (declared in its `.agentsmith/context.yaml`, or pinned per language via `projects.X.sandbox.images`).

You don't pre-create anything. Sizing is pipeline-aware: code-changing pipelines use the repo's declared `stack.resources` (clamped to a hard ceiling), scans and other non-build pipelines get a light fixed profile. When a run finishes — success, failure, cancel — the pods are deleted; a force-killed cancel releases them immediately.

Since p0331 a run doesn't even provision every repo in the project: the `ScopeRepos` step reads the ticket first and spawns sandboxes only for the affected repos. Fewer pods times shorter lifetimes is the biggest cost lever in this setup.

## Updating

Bump the tag in the Deployment images and the `deployment:` pin in the ConfigMap together, then:

```bash
kubectl apply -f deploy/k8s/
kubectl -n agentsmith rollout status deployment/agentsmith-server
```

`RollingUpdate` is the default strategy. The migrate init-container applies any new migrations before the new server accepts traffic. In-flight runs continue in their own pods until they finish.

## Resources

The server itself is cheap — it waits on LLM calls and shuffles events. The interesting sizing is per run:

- **The spawned orchestrator pod** runs the LLM loop and compiles nothing. It ships sized honestly (100m / 512Mi requests, 500m / 2Gi limits via the `JobSpawner__Resources__*` env values) because it's the longest-lived pod of every run.
- **Build sandboxes** default to a 1Gi request with a 4Gi limit as the OOM guard. Keep requests honest, not minimal — see the warning below.

## Capacity quota: count requests, not limits

The capacity probe reads the namespace `ResourceQuota` and admits a run only when its whole footprint (orchestrator pod + one sandbox per repo) still fits. It compares **only the quota keys present in `status.hard`** — so the quota's shape decides what "capacity" means. A run that doesn't fit is queued (strict FIFO, one entry per ticket, visible amber in the dashboard with its position) and launched when capacity frees.

Quota the namespace on **requests**, not limits. Requests are what the scheduler packs nodes by — i.e. what the cluster actually provisions and what costs money. A quota on `limits.memory` reserves the theoretical worst case for a pod's whole runtime: five default pods "use" 20Gi of quota while their real reservation is a fraction of that, and runs queue behind capacity nobody is consuming.

Worked example for an 8-CPU / 20Gi-class cluster (adjust to yours; leave headroom for the server, Redis, dashboard, and system pods):

```yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: agentsmith-capacity
  namespace: agentsmith
spec:
  hard:
    requests.cpu: "6"        # ~75% of 8 CPUs — headroom for the platform pods
    requests.memory: 14Gi    # ~70% of 20Gi — same reasoning
    pods: "12"               # the deterministic backpressure knob
```

No `limits.*` keys: limits stay on the pods purely as the OOM guard, they no longer count against capacity. The `pods` key is the deterministic backpressure knob — the Kubernetes analog of Docker's `max_concurrent_sandboxes`.

Two warnings:

- **Keep requests honest, not minimal.** Node-pressure eviction kills Burstable pods ranked by usage-above-request first. A build sandbox declared at 512Mi that peaks at 3–4Gi during `dotnet build` is the prime eviction victim — that resurrects the "sandbox vanished" failure class. The build-sandbox default stays at a 1Gi request with a 4Gi limit as the OOM guard.
- **The quota lives in your cluster config, not in this repo.** Applying the requests-based quota is a **coordinated operator step**: land it together with the orchestrator env values in `deploy/k8s/8-deployment-server.yaml`, in whatever repo manages your namespace.

Each finished run shows its **reserved capacity-time** (memory request × pod lifetime, in Gi·minutes) next to the LLM cost on the run detail page — reservation, not measured consumption — so you can see whether a run was expensive in tokens or in pods. More on the [capacity page](../reference/operations/capacity.md).

## Next

- [Webhooks](../trigger-it/webhooks.md) — point them at the ingress URL.
- [docker-compose](docker-compose.md) — the simpler version for one host.
- [Capacity & queueing](../reference/operations/capacity.md) — admission, the FIFO queue, cancel semantics.
- [Dashboard](../reference/operations/dashboard.md) — what all those pods are doing.
