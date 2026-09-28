# Host it: Kubernetes

For shared use and production. The server as a Deployment, Redis alongside, sandbox pods created on demand and disposed when each run ends. No CRDs, no operators — stock Kubernetes objects. The manifests ship in the repo under `deploy/k8s/`, numbered in apply order:

```
deploy/k8s/
├── 1-namespace.yaml
├── 2-rbac.yaml                  # ServiceAccount + Role + RoleBinding (pod create/delete)
├── 3-configmap.yaml             # the bootstrap agentsmith.yml (persistence + secret names)
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

- The server survives node failures because Kubernetes reschedules it, and the run history survives everything because it lives in a relational database (SQLite on a PVC by default; point `persistence:` at PostgreSQL, MySQL or SQL Server for anything shared, and the same init-container migrates it). To keep the database password out of the ConfigMap, set `AGENTSMITH_PERSISTENCE_PROVIDER` and `AGENTSMITH_PERSISTENCE_CONNECTION` as environment variables on both the server and the migrate init-container, for example from the Secret. Set together, they replace the `persistence:` block; one without the other is refused with a startup finding.
- Sandbox pods created per repo and toolchain image per run, then deleted. The server's `ServiceAccount` has permission to create / delete pods in its own namespace — that's the whole RBAC story.
- Real capacity control: a `ResourceQuota` on the namespace turns "too many runs" into a FIFO queue instead of failures (see the capacity section below).
- The dashboard as its own Deployment + Service, port 3000 behind the Service.

## The shape of the server pod

The load-bearing details from `8-deployment-server.yaml`, so you know what you're looking at:

- **An init-container runs `agentsmith database migrate --config /app/config/agentsmith.yml`** before the server starts. Migrations are applied exactly there — the server never migrates its own database on startup, deliberately. It shares the persistence volume with the server (and for an external DB it migrates over the connection string instead).
- The server container listens on **8081** (`/health`, which answers for liveness and readiness alike and stays `200` while a subsystem is degraded, so the pod that reports the fault is never pulled out of the service). The body lists each background subsystem with its state and reason, and the startup preflight — the same checks as `agent-smith doctor` — runs warn-only in the background and reports there too; a degraded tracker shows up there instead of blocking startup.
- The ConfigMap mounted at **`/app/config/agentsmith.yml`** carries the bootstrap slice only, meaning `persistence:` and `secrets:`. Everything else (agents, trackers, repos, projects, and the global settings) lives in the database and is edited in the dashboard's Config studio. Putting a full catalog into this ConfigMap does nothing, because the server reads those two blocks and ignores the rest. See [Where configuration lives](../configure-it/index.md).
- Secrets come from the `agentsmith-secrets` Secret (`REDIS_URL`, provider keys, tracker tokens, webhook secrets, optional Slack/Teams tokens). The ConfigMap names them, the Secret holds the values.
- `SPAWNER_TYPE` is `kubernetes` by default in-cluster: each triggered run is spawned as its own short-lived orchestrator pod (the CLI image), which in turn creates the sandbox pods. That's why the quota math below counts "orchestrator + its sandboxes" per run.
- The images are one release: `holgerleichsenring/agent-smith-server`, `holgerleichsenring/agent-smith-cli` and `holgerleichsenring/agentsmith-dashboard` carry the same tag in the manifests, and the same number goes into **Configuration → Deployment** in the studio (`deployment.version`), which the spawned orchestrator pod's image is built from. The sandbox-agent image (`holgerleichsenring/agent-smith-sandbox-agent`) needs no pin: its tag is derived from the release the server is. Pin `sandbox.agent_version` only to run a different tag on purpose; a pin is reported as an advisory finding, never refused.

- Skills need no pin: every release embeds the catalog it was tested with. The `skills` volume is an `emptyDir` the embedded catalog materializes into at startup.

## First configuration

The init-container creates the schema. It does not seed configuration, so a fresh deployment comes up with an empty catalog.

Fill it either from an existing file, running the CLI image as a one-shot pod, or through the dashboard:

```bash
kubectl -n agentsmith port-forward svc/agentsmith-dashboard 3000:3000
```

Then switch the rail to **Configuration** and build the catalog. When you script the import instead, call `dotnet AgentSmith.Cli.dll config import ...` directly rather than the `agentsmith` entrypoint. The entrypoint drops privileges with gosu, and that fights a read only, non root `securityContext`.

## Webhooks

The Ingress (`9-ingress.yaml`) routes your public hostname to the server Service. Point your tracker webhooks at `https://agent-smith.your-domain.example/webhook` — endpoint details and per-tracker setup on the [webhooks page](../trigger-it/webhooks.md). The webhook shared secrets (`GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_TOKEN`, `AZDO_WEBHOOK_SECRET`) go into the Secret next to the API keys.

## Sandbox pods

The server creates one pod per repo and toolchain image per run. Contexts of one repo that share an image share one pod, sized to the largest resource envelope among them; a repo whose contexts need two different images gets two pods. Each pod has an init-container that copies the sandbox-agent binary into a shared `emptyDir`, then the main toolchain container starts and the agent binary takes over the entrypoint. The toolchain image comes from, in order: `projects.<name>.sandbox.toolchain_image`, `projects.<name>.sandbox.images.<language>`, the context's `stack.image` in `.agentsmith/contexts/<name>/context.yaml`, a built-in per-language table, and finally a generic image that carries git and no toolchain (with a warning that nothing can be built there).

A repository is cloned inside its own sandbox, so the image has to carry git. One that doesn't fails the checkout with an error that says so, instead of a bare exit code.

You don't pre-create anything. Sizing is pipeline-aware: code-changing pipelines use the repo's declared `stack.resources` (clamped to a hard ceiling), scans and other non-build pipelines get a light fixed profile. When a run finishes (success, failure, cancel) the pods are deleted; a force-killed cancel releases them immediately. A run doesn't provision every repo in the project either: the `ScopeRepos` step reads the ticket first and spawns sandboxes only for the affected repos.

Pods are labelled with the deployment that owns them. The owner identity is derived from the Redis endpoint; `SANDBOX_OWNER_ID` names it explicitly. A corpse reaper deletes this deployment's pods whose run is no longer live, on a timer and again at admission, so a crashed server doesn't leave pods holding quota. Two deployments in one namespace never touch each other's pods.

### Which registries a toolchain image may come from

The context's `stack.image` is written by a model, so it is held to a registry boundary before it is used:

```yaml
sandbox:
  allowed_registries: ["mcr.microsoft.com/", "ghcr.io/", "registry.acme-org.example/"]
  allow_docker_hub_library: false
```

`allowed_registries` is a list of image-reference prefixes. Left empty it means the built-in default, `mcr.microsoft.com/` and `ghcr.io/`. `allow_docker_hub_library` decides whether an official Docker Hub image with no namespace (`node:20-bookworm`) is trusted. Unset, it follows the list: trusted while you name no registries, refused once you do, because a named list is a narrowing. A `stack.image` outside the boundary is skipped with a warning and the chain falls through to the next source. An image you name yourself in `toolchain_image` or `images` is your choice and is not checked.

### Pulling from a private registry

```yaml
sandbox:
  image_pull_secrets: [acme-registry-pull]
```

`image_pull_secrets` names Kubernetes image pull secrets you created in the namespace. They go on every sandbox pod, init container included, so the agent image and the toolchain image can come from different credentialed registries. The list is global; a project cannot name its own. Kubernetes only: the Docker backend pulls without credentials and says so when a pull fails.

### Credentials inside the sandbox

A build step that needs a credential (a private feed, a CLI login) gets it from a Kubernetes Secret you own. The project names the reference; the value never passes through Agent Smith, Redis, the context file or the model:

```yaml
projects:
  todolist:
    sandbox:
      secrets:
        env:
          FEED_TOKEN: "todolist-build:feed-token"     # secretName:key
        files:
          - mount: /secrets/signing.key
            secret: todolist-build
            key: signing-key
```

`env` entries become environment variables from a `secretKeyRef`; `files` entries are mounted read-only at `mount`. The command that uses them lives in the context's `prerequisites`. The run preflight checks the declaration without touching the cluster: a reference without a single `:`, a duplicate or a clashing mount is named before anything starts. Once the pod is up, a second check asserts by name that every declared variable and file arrived, and fails naming the missing one, never its value. On the Docker and in-process backends nothing is injected, and the preflight reports "NOT INJECTED" as a warning instead of failing.

## Updating

Bump the tag in the manifests' images (server, the migrate init-container, the dashboard, `AGENTSMITH_IMAGE`) and the Deployment setting in the studio together, then:

```bash
kubectl apply -f deploy/k8s/
kubectl -n agentsmith rollout status deployment/agentsmith-server
```

`RollingUpdate` is the default strategy. The migrate init-container applies any new migrations before the new server accepts traffic. In-flight runs continue in their own pods until they finish.

## Resources

The server itself is cheap on CPU — it waits on LLM calls and shuffles events — but it is **not** cheap on memory: ASP.NET + SignalR + EF + the skills catalog + live event streams need room. Give the server pod a **request of at least 512Mi and a limit of 1–1.5Gi**. Below that it OOMKills under normal load, and every OOM-restart reaps the in-flight run (surfacing as a bogus "cancelled"), truncates the durable event trail, and leaves the run's sandbox pods to the corpse reaper. The startup preflight WARNs when the pod's memory ceiling is under the 512Mi floor. Remember the namespace `ResourceQuota` counts the server's request/limit too.

The interesting sizing is per run:

- **The spawned orchestrator pod** runs the LLM loop and compiles nothing. It ships sized honestly (100m / 512Mi requests, 500m / 2Gi limits via the `JobSpawner__Resources__*` env values) because it's the longest-lived pod of every run.
- **Build sandboxes** default to a 1Gi request with a 4Gi limit as the OOM guard. Keep requests honest, not minimal — see the warning below.

## Capacity quota: count requests, not limits

The capacity probe reads the namespace `ResourceQuota` and admits a run only when its whole footprint (orchestrator pod + one sandbox per repo and toolchain image) still fits. It compares **only the quota keys present in `status.hard`** — so the quota's shape decides what "capacity" means. A run that doesn't fit is queued (strict FIFO, one entry per ticket, visible amber in the dashboard with its position) and launched when capacity frees.

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

That analogy is the whole relationship: `max_concurrent_sandboxes` in the `sandbox:` settings (`maxConcurrentSandboxes` on the wire and in the dashboard, `MaxConcurrentSandboxes` in the stored document and the C# model, `SANDBOX_MAX_CONCURRENT` as the environment-variable fallback for an empty store) is read by the **Docker** capacity probe only. Here it does nothing — this quota is what bounds your sandboxes. See [where the bound is set](../reference/operations/capacity.md#where-the-concurrent-sandbox-bound-is-set).

Two warnings:

- **Keep requests honest, not minimal.** Node-pressure eviction kills Burstable pods ranked by usage-above-request first. A build sandbox declared at 512Mi that peaks at 3–4Gi during `dotnet build` is the prime eviction victim — that resurrects the "sandbox vanished" failure class. The build-sandbox default stays at a 1Gi request with a 4Gi limit as the OOM guard.
- **The quota lives in your cluster config, not in this repo.** Applying the requests-based quota is a **coordinated operator step**: land it together with the orchestrator env values in `deploy/k8s/8-deployment-server.yaml`, in whatever repo manages your namespace.

Each finished run shows its **reserved capacity-time** (memory request × pod lifetime, in Gi·minutes) next to the LLM cost on the run detail page — reservation, not measured consumption — so you can see whether a run was expensive in tokens or in pods. More on the [capacity page](../reference/operations/capacity.md).

## Next

- [Webhooks](../trigger-it/webhooks.md) — point them at the ingress URL.
- [docker-compose](docker-compose.md) — the simpler version for one host.
- [Capacity & queueing](../reference/operations/capacity.md) — admission, the FIFO queue, cancel semantics.
- [Dashboard](../reference/operations/dashboard.md) — what all those pods are doing.
