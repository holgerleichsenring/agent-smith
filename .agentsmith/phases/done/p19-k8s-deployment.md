# Phase 19: K8s Manifests + Dispatcher Dockerfile

## Goal

Make Agent Smith fully deployable on Kubernetes.
This phase delivers everything needed to run the complete stack in a cluster:
- Dispatcher as a long-running Deployment
- Redis as an ephemeral in-memory Deployment (no PV)
- Agent containers spawned as K8s Jobs per request
- All secrets managed via K8s Secret
- A dedicated Dockerfile for the Dispatcher

No new application logic. Pure infrastructure.

---

## What Gets Deployed

```
Namespace: agentsmith
│
├── Deployment: agentsmith-dispatcher    ← always-on, handles Slack/Teams
├── Service: agentsmith-dispatcher       ← ClusterIP + optional Ingress
│
├── Deployment: redis                    ← ephemeral, in-memory, no PV
├── Service: redis                       ← ClusterIP, port 6379
│
├── Secret: agentsmith-secrets           ← all tokens and API keys
├── ConfigMap: agentsmith-config         ← agentsmith.yml content
│
└── Jobs: agentsmith-{jobId}             ← spawned on demand, TTL 300s
```

---

## Phase 19 Steps

| Step | File | Description |
|------|------|-------------|
| 19-1 | `phase19-dispatcher-dockerfile.md` | Dispatcher Dockerfile (multi-stage, ASP.NET Core) |
| 19-2 | `phase19-redis.md` | Redis Deployment + Service (no PV, maxmemory 256mb) |
| 19-3 | `phase19-secrets.md` | K8s Secret schema + creation script |
| 19-4 | `phase19-configmap.md` | ConfigMap for agentsmith.yml |
| 19-5 | `phase19-dispatcher-deployment.md` | Dispatcher Deployment + Service + optional Ingress |
| 19-6 | `phase19-rbac.md` | ServiceAccount + Role + RoleBinding for Job spawning |
| 19-7 | `phase19-kustomize.md` | Kustomize overlay (base + overlays/dev + overlays/prod) |
| 19-8 | `phase19-local-test.md` | Local K8s test guide (Docker Desktop / kind) |

---

## Directory Structure

```
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── redis/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── dispatcher/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── rbac/
│   │   ├── serviceaccount.yaml
│   │   ├── role.yaml
│   │   └── rolebinding.yaml
│   └── configmap/
│       └── agentsmith-config.yaml
├── overlays/
│   ├── dev/
│   │   ├── kustomization.yaml
│   │   └── patch-dispatcher-dev.yaml   ← imagePullPolicy: Never, 1 replica
│   └── prod/
│       ├── kustomization.yaml
│       └── patch-dispatcher-prod.yaml  ← imagePullPolicy: Always, 2 replicas
└── secret-template.yaml                ← template only, never committed
```

---

## Dispatcher Dockerfile

Separate from the agent container Dockerfile.
The Dispatcher is an ASP.NET Core web app, not a CLI tool.

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj           src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj     src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Server/AgentSmith.Server.csproj   src/AgentSmith.Server/
RUN dotnet restore src/AgentSmith.Server/AgentSmith.Server.csproj
COPY src/ src/
RUN dotnet publish src/AgentSmith.Server -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN groupadd --gid 1000 dispatcher && \
    useradd --uid 1000 --gid 1000 --no-create-home dispatcher
COPY --from=build /app/publish .
COPY config/ ./config/
RUN mkdir -p /tmp/agentsmith && chown dispatcher:dispatcher /tmp/agentsmith
USER dispatcher
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "AgentSmith.Server.dll"]
```

---

## Redis Manifest

No PersistentVolume. In-memory only. If Redis restarts, in-flight jobs
are considered lost (K8s will restart the Job containers too).

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: agentsmith
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          args:
            - "--maxmemory"
            - "256mb"
            - "--maxmemory-policy"
            - "allkeys-lru"
            - "--save"
            - ""        # disable persistence
          ports:
            - containerPort: 6379
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "500m"
              memory: "384Mi"
```

---

## RBAC for Job Spawning

The Dispatcher needs permission to create and delete K8s Jobs in its namespace.
Cluster-wide permissions are NOT required — namespace-scoped Role is sufficient.

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: agentsmith-job-manager
  namespace: agentsmith
rules:
  - apiGroups: ["batch"]
    resources: ["jobs"]
    verbs: ["create", "delete", "get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
```

---

## Secret Schema

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: agentsmith-secrets
  namespace: agentsmith
type: Opaque
stringData:
  anthropic-api-key: ""
  openai-api-key: ""
  gemini-api-key: ""
  github-token: ""
  azure-devops-token: ""
  gitlab-token: ""
  jira-token: ""
  jira-email: ""
  slack-bot-token: ""
  slack-signing-secret: ""
  redis-url: "redis://redis:6379"
```

Never commit a populated secret. Use `kubectl create secret` or Sealed Secrets / External Secrets Operator in production.

---

## Dispatcher Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  replicas: 1
  selector:
    matchLabels:
      app: agentsmith-dispatcher
  template:
    spec:
      serviceAccountName: agentsmith
      containers:
        - name: dispatcher
          image: agentsmith-dispatcher:latest
          ports:
            - containerPort: 8080
          env:
            - name: REDIS_URL
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: redis-url
            - name: SLACK_BOT_TOKEN
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-bot-token
            - name: SLACK_SIGNING_SECRET
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-signing-secret
            - name: K8S_NAMESPACE
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
            - name: AGENTSMITH_IMAGE
              value: "agentsmith:latest"
          volumeMounts:
            - name: config
              mountPath: /app/config
              readOnly: true
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
      volumes:
        - name: config
          configMap:
            name: agentsmith-config
```

---

## ConfigMap

`agentsmith.yml` is mounted into the Dispatcher container via ConfigMap.
This avoids baking environment-specific config into the image.

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agentsmith-config
  namespace: agentsmith
data:
  agentsmith.yml: |
    projects:
      my-project:
        source:
          type: GitHub
          ...
    pipelines:
      fix-bug:
        commands:
          - FetchTicketCommand
          ...
    secrets:
      github_token: ${GITHUB_TOKEN}
      ...
```

In practice: generate this ConfigMap from your local `config/agentsmith.yml`:

```bash
kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml=config/agentsmith.yml \
  -n agentsmith \
  --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
```

---

## Kustomize Overlays

### Dev (Docker Desktop / kind)
- `imagePullPolicy: Never` (uses locally built images)
- 1 replica
- `nodePort` service for easy local access

### Prod
- `imagePullPolicy: Always`
- 2 replicas for the Dispatcher
- `ClusterIP` service + Ingress with TLS

---

## Local Test Guide (Docker Desktop)

```bash
# 1. Enable Kubernetes in Docker Desktop settings

# 2. Build both images locally
docker build -t agentsmith:latest .
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .

# 3. Create namespace
kubectl apply -f k8s/base/namespace.yaml

# 4. Create secret (fill in your values)
cp k8s/secret-template.yaml k8s/secret-local.yaml
# Edit k8s/secret-local.yaml with your tokens
kubectl apply -f k8s/secret-local.yaml

# 5. Apply base manifests
kubectl apply -k k8s/overlays/dev

# 6. Check everything is running
kubectl get pods -n agentsmith

# 7. Forward the dispatcher port
kubectl port-forward svc/agentsmith-dispatcher 8080:8080 -n agentsmith

# 8. Test health
curl http://localhost:8080/health

# 9. Use ngrok to expose to Slack
ngrok http 8080
```

---

## Success Criteria

- [ ] `kubectl apply -k k8s/overlays/dev` brings up full stack
- [ ] `kubectl get pods -n agentsmith` shows redis + dispatcher Running
- [ ] `/health` returns 200
- [ ] Slack message "fix #54 in agent-smith-test" spawns a K8s Job
- [ ] `kubectl get jobs -n agentsmith` shows the job appearing
- [ ] Progress messages appear in Slack
- [ ] Job pod completes and is cleaned up after TTL
- [ ] `kubectl apply -k k8s/overlays/prod` works for prod deployment

---

## Dependencies

- Phase 18 complete (Dispatcher service, Slack adapter, Redis bus)
- Docker Desktop with Kubernetes enabled (for local testing)
- `kubectl` and `kustomize` installed
- ngrok (for Slack webhook during local testing)

---

# Phase 19 – Step 4: ConfigMap for agentsmith.yml

## Goal

Mount the `agentsmith.yml` configuration into the Dispatcher container via a
Kubernetes ConfigMap. This avoids baking environment-specific config into the
container image and allows config changes without rebuilding.

---

## File

`k8s/base/configmap/agentsmith-config.yaml`

---

## Manifest

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agentsmith-config
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: config
data:
  agentsmith.yml: |
    # This ConfigMap is generated from config/agentsmith.yml.
    # Do not edit manually. Regenerate with:
    #
    #   kubectl create configmap agentsmith-config \
    #     --from-file=agentsmith.yml=config/agentsmith.yml \
    #     -n agentsmith \
    #     --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
    #
    projects:
      my-project:
        source:
          type: GitHub
          url: https://github.com/yourorg/your-repo
          auth: token
        tickets:
          type: GitHub
          url: https://github.com/yourorg/your-repo
          auth: token
        agent:
          type: Claude
          model: claude-sonnet-4-20250514
          retry:
            max_retries: 5
            initial_delay_ms: 2000
            backoff_multiplier: 2.0
            max_delay_ms: 60000
          cache:
            enabled: true
            strategy: automatic
          compaction:
            enabled: true
            threshold_iterations: 8
            max_context_tokens: 80000
            keep_recent_iterations: 3
            summary_model: claude-haiku-4-5-20251001
          models:
            scout:
              model: claude-haiku-4-5-20251001
              max_tokens: 4096
            primary:
              model: claude-sonnet-4-20250514
              max_tokens: 8192
        pipeline: fix-bug
        coding_principles_path: ./config/coding-principles.md

    pipelines:
      fix-bug:
        commands:
          - FetchTicketCommand
          - CheckoutSourceCommand
          - LoadCodingPrinciplesCommand
          - AnalyzeCodeCommand
          - GeneratePlanCommand
          - ApprovalCommand
          - AgenticExecuteCommand
          - TestCommand
          - CommitAndPRCommand

    secrets:
      github_token: ${GITHUB_TOKEN}
      anthropic_api_key: ${ANTHROPIC_API_KEY}
      azure_devops_token: ${AZURE_DEVOPS_TOKEN}
      openai_api_key: ${OPENAI_API_KEY}
      gemini_api_key: ${GEMINI_API_KEY}
```

---

## How It Is Mounted

The Dispatcher `Deployment` mounts this ConfigMap as a volume at `/app/config`:

```yaml
volumeMounts:
  - name: config
    mountPath: /app/config
    readOnly: true

volumes:
  - name: config
    configMap:
      name: agentsmith-config
```

The Dispatcher reads `config/agentsmith.yml` via `IConfigurationLoader` at startup
and on every `list tickets` / `create ticket` request.

---

## Regenerating from Local Config

When you change your local `config/agentsmith.yml`, regenerate the ConfigMap with:

```bash
kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml=config/agentsmith.yml \
  -n agentsmith \
  --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
```

Then apply with:

```bash
kubectl apply -k k8s/overlays/dev   # or prod
```

The Dispatcher pod must be restarted to pick up config changes
(ConfigMap updates are not hot-reloaded):

```bash
kubectl rollout restart deployment/agentsmith-dispatcher -n agentsmith
```

---

## Design Notes

- **Never bake config into the image.** Project URLs, token types, pipeline definitions
  and model choices all belong in the ConfigMap, not in the Dockerfile.
- **Secrets stay in the K8s Secret.** The ConfigMap holds only non-sensitive structure.
  All `${ENV_VAR}` references in `agentsmith.yml` are resolved at runtime from
  environment variables injected via the `agentsmith-secrets` Secret.
- **One ConfigMap per environment.** The dev and prod overlays can each generate their
  own ConfigMap from different source files if needed.
- **Placeholder content in base.** The `k8s/base/configmap/agentsmith-config.yaml`
  contains a working template. Real projects override it by regenerating from their
  own `config/agentsmith.yml`.

---

## Definition of Done

- [ ] `k8s/base/configmap/agentsmith-config.yaml` exists and is valid YAML
- [ ] ConfigMap is referenced in `k8s/base/kustomization.yaml`
- [ ] Dispatcher Deployment mounts it at `/app/config`
- [ ] `kubectl apply -k k8s/overlays/dev` applies the ConfigMap without errors
- [ ] Dispatcher can read `config/agentsmith.yml` inside the pod

---

# Phase 19 – Step 5: Dispatcher Deployment + Service

## Goal

Deploy the `AgentSmith.Server` as a long-running Kubernetes `Deployment`
with a `ClusterIP` Service, liveness/readiness probes, and full secret injection.

---

## Files

### `k8s/base/dispatcher/deployment.yaml`

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  labels:
    app: agentsmith-dispatcher
    app.kubernetes.io/name: agentsmith-dispatcher
    app.kubernetes.io/component: dispatcher
spec:
  replicas: 1
  selector:
    matchLabels:
      app: agentsmith-dispatcher
  template:
    metadata:
      labels:
        app: agentsmith-dispatcher
    spec:
      serviceAccountName: agentsmith
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        runAsGroup: 1000
        fsGroup: 1000
      containers:
        - name: dispatcher
          image: agentsmith-dispatcher:latest
          imagePullPolicy: IfNotPresent
          ports:
            - name: http
              containerPort: 8080
              protocol: TCP
          env:
            - name: ASPNETCORE_URLS
              value: "http://+:8080"
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: REDIS_URL
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: redis-url
            - name: SLACK_BOT_TOKEN
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-bot-token
            - name: SLACK_SIGNING_SECRET
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: slack-signing-secret
            - name: K8S_NAMESPACE
              valueFrom:
                fieldRef:
                  fieldPath: metadata.namespace
            - name: AGENTSMITH_IMAGE
              value: "agentsmith:latest"
            - name: IMAGE_PULL_POLICY
              value: "IfNotPresent"
            - name: K8S_SECRET_NAME
              value: "agentsmith-secrets"
          volumeMounts:
            - name: config
              mountPath: /app/config
              readOnly: true
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 3
          resources:
            requests:
              cpu: "100m"
              memory: "256Mi"
            limits:
              cpu: "500m"
              memory: "512Mi"
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: false
      volumes:
        - name: config
          configMap:
            name: agentsmith-config
      restartPolicy: Always
```

---

### `k8s/base/dispatcher/service.yaml`

```yaml
apiVersion: v1
kind: Service
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  labels:
    app: agentsmith-dispatcher
    app.kubernetes.io/name: agentsmith-dispatcher
    app.kubernetes.io/component: dispatcher
spec:
  type: ClusterIP
  selector:
    app: agentsmith-dispatcher
  ports:
    - name: http
      port: 80
      targetPort: 8080
      protocol: TCP
```

The base service is `ClusterIP` only. External access is handled by overlay-specific
patches (NodePort for dev, Ingress for prod).

---

## Design Decisions

### One Dispatcher Replica (base)
The base manifest sets `replicas: 1`. The prod overlay scales to 2.
The dispatcher is stateless (all state lives in Redis), so scaling is safe.

### ServiceAccount
`serviceAccountName: agentsmith` — the same service account used by the RBAC
Role+RoleBinding that grants Job creation rights. Defined in `rbac/serviceaccount.yaml`.

### Config Volume
`agentsmith.yml` is mounted from the `agentsmith-config` ConfigMap.
The Dispatcher reads it at runtime via `IConfigurationLoader` for the list/create fast-path.
Changing the config requires updating the ConfigMap and restarting the pod.

### Secret Injection
All credentials come from `agentsmith-secrets` via `secretKeyRef`.
The pod never has access to the raw secret file, only the specific keys it needs.

### Health Probes
- **Readiness**: begins checking after 5s; pod receives traffic only when `/health` returns 200
- **Liveness**: begins checking after 15s; restarts pod if 3 consecutive checks fail
- Both use `GET /health` which is implemented in `Program.cs` and returns immediately

### Resource Limits
Base limits are conservative (500m CPU / 512Mi RAM). The dispatcher is mostly
I/O bound (Redis + Slack API calls). The prod overlay increases these for 2-replica headroom.

### `readOnlyRootFilesystem: false`
Required because ASP.NET Core writes temp files to `/tmp`. An alternative is to mount
an emptyDir at `/tmp`, but `false` is simpler and acceptable for this workload.

---

## Environment Variables Summary

| Variable | Source | Purpose |
|----------|--------|---------|
| `ASPNETCORE_URLS` | hardcoded | Bind to port 8080 |
| `ASPNETCORE_ENVIRONMENT` | hardcoded | `Production` (overridden in dev overlay) |
| `REDIS_URL` | secret | Redis connection string |
| `SLACK_BOT_TOKEN` | secret | Slack Web API token |
| `SLACK_SIGNING_SECRET` | secret | Slack request verification |
| `K8S_NAMESPACE` | fieldRef | Namespace for spawning jobs (auto-detected) |
| `AGENTSMITH_IMAGE` | hardcoded | Image for spawned K8s Jobs |
| `IMAGE_PULL_POLICY` | hardcoded | Pull policy for spawned jobs |
| `K8S_SECRET_NAME` | hardcoded | Secret name injected into spawned jobs |

---

## Definition of Done

- [ ] `deployment.yaml` applies cleanly to the cluster
- [ ] `service.yaml` creates a ClusterIP service on port 80 → 8080
- [ ] Pod starts with non-root UID 1000
- [ ] Config volume is mounted at `/app/config`
- [ ] Liveness and readiness probes pass within 30s of pod start
- [ ] `kubectl port-forward svc/agentsmith-dispatcher 8080:80 -n agentsmith`
      then `curl http://localhost:8080/health` returns `{"status":"ok",...}`


---

# Phase 19 – Step 1: Dispatcher Dockerfile

## Goal

Create a production-ready multi-stage Dockerfile for the `AgentSmith.Server`
ASP.NET Core web application. Separate from the existing `Dockerfile` which builds
the CLI agent container.

---

## File: `Dockerfile.dispatcher`

Located at the repository root, alongside `Dockerfile`.

### Multi-Stage Build

**Stage 1: Build (`mcr.microsoft.com/dotnet/sdk:8.0`)**

- Copy only `.csproj` files first (layer caching for `dotnet restore`)
- Projects to include:
  - `AgentSmith.Domain`
  - `AgentSmith.Contracts`
  - `AgentSmith.Application`
  - `AgentSmith.Infrastructure`
  - `AgentSmith.Server`
- `dotnet restore src/AgentSmith.Server/AgentSmith.Server.csproj`
- Copy all source
- `dotnet publish src/AgentSmith.Server -c Release -o /app/publish --no-restore`

**Stage 2: Runtime (`mcr.microsoft.com/dotnet/aspnet:8.0`)**

- `aspnet` image (not `runtime`) — required for ASP.NET Core
- Install `curl` for the `HEALTHCHECK` command
- Create non-root user `dispatcher` (uid/gid 1000)
- Copy published output from build stage
- Copy `config/` directory (default config, overridden in K8s via ConfigMap)
- Create `/tmp/agentsmith` with correct ownership
- `USER dispatcher`
- `EXPOSE 8080`
- Set `ASPNETCORE_URLS=http://+:8080` and `ASPNETCORE_ENVIRONMENT=Production`
- `HEALTHCHECK` using `curl -f http://localhost:8080/health`
- `ENTRYPOINT ["dotnet", "AgentSmith.Server.dll"]`

### Full Dockerfile

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (layer caching)
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj                   src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj             src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj         src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj   src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Server/AgentSmith.Server.csproj           src/AgentSmith.Server/

RUN dotnet restore src/AgentSmith.Server/AgentSmith.Server.csproj

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/AgentSmith.Server -c Release -o /app/publish --no-restore

# ---

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health check
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user
RUN groupadd --gid 1000 dispatcher && \
    useradd --uid 1000 --gid 1000 --no-create-home dispatcher

COPY --from=build /app/publish .
COPY config/ ./config/

RUN mkdir -p /tmp/agentsmith && chown dispatcher:dispatcher /tmp/agentsmith

USER dispatcher

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgentSmith.Server.dll"]
```

---

## Key Differences vs `Dockerfile` (CLI agent)

| Aspect | `Dockerfile` (agent) | `Dockerfile.dispatcher` |
|--------|---------------------|------------------------|
| Base runtime | `dotnet/runtime:8.0` | `dotnet/aspnet:8.0` |
| Entry project | `AgentSmith.Cli` | `AgentSmith.Server` |
| User | `agentsmith` (uid 1000) | `dispatcher` (uid 1000) |
| SSH mount | Yes (`~/.ssh`) | No |
| Port | 8080 (webhook mode) | 8080 (always) |
| ENTRYPOINT | `AgentSmith.Cli.dll` | `AgentSmith.Server.dll` |
| curl install | No | Yes (for HEALTHCHECK) |

---

## Build Commands

```bash
# Build agent image
docker build -t agentsmith:latest .

# Build dispatcher image
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .
```

---

## Local Test

```bash
docker run --rm \
  -e REDIS_URL=redis://localhost:6379 \
  -e SLACK_BOT_TOKEN=xoxb-test \
  -p 8080:8080 \
  agentsmith-dispatcher:latest

curl http://localhost:8080/health
# → {"status":"ok","timestamp":"..."}
```

---

## Definition of Done

- [ ] `Dockerfile.dispatcher` at repository root
- [ ] Multi-stage build: sdk:8.0 → aspnet:8.0
- [ ] Non-root user `dispatcher` (uid 1000)
- [ ] `curl` installed for HEALTHCHECK
- [ ] `config/` directory copied into image
- [ ] `EXPOSE 8080`
- [ ] `HEALTHCHECK` using `/health` endpoint
- [ ] `docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .` succeeds
- [ ] `docker run` → `curl /health` returns 200

---

# Phase 19 – Step 7: Kustomize Base + Overlays

## Goal

Wire all K8s manifests together using Kustomize so the full stack can be deployed
with a single command in both local (dev) and production environments.

---

## Directory Structure

```
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── redis/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── dispatcher/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── rbac/
│   │   ├── serviceaccount.yaml
│   │   ├── role.yaml
│   │   └── rolebinding.yaml
│   └── configmap/
│       └── agentsmith-config.yaml
├── overlays/
│   ├── dev/
│   │   ├── kustomization.yaml
│   │   ├── patch-dispatcher-dev.yaml
│   │   └── patch-service-nodeport.yaml   ← NodePort for local browser access
│   └── prod/
│       ├── kustomization.yaml
│       ├── patch-dispatcher-prod.yaml
│       └── ingress.yaml                  ← Ingress + TLS for production
└── secret-template.yaml                  ← template only, never committed
```

---

## Base Kustomization

**`k8s/base/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

resources:
  - namespace.yaml
  - redis/deployment.yaml
  - redis/service.yaml
  - dispatcher/deployment.yaml
  - dispatcher/service.yaml
  - rbac/serviceaccount.yaml
  - rbac/role.yaml
  - rbac/rolebinding.yaml
  - configmap/agentsmith-config.yaml
```

The base is environment-agnostic. No image tags, no pull policies, no replica
counts are environment-specific — those are all patched in overlays.

---

## Dev Overlay

**`k8s/overlays/dev/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

bases:
  - ../../base

patches:
  - path: patch-dispatcher-dev.yaml
    target:
      kind: Deployment
      name: agentsmith-dispatcher
  - path: patch-service-nodeport.yaml
    target:
      kind: Service
      name: agentsmith-dispatcher
```

**`k8s/overlays/dev/patch-dispatcher-dev.yaml`**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  replicas: 1
  template:
    spec:
      containers:
        - name: dispatcher
          imagePullPolicy: Never      # uses locally built image (Docker Desktop / kind)
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Development"
            - name: IMAGE_PULL_POLICY
              value: "Never"          # passed to JobSpawner for agent containers
```

**`k8s/overlays/dev/patch-service-nodeport.yaml`**

Exposes the dispatcher on a static local port so `curl localhost:30080/health`
works without running `kubectl port-forward`.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  type: NodePort
  ports:
    - name: http
      port: 80
      targetPort: 8080
      nodePort: 30080
      protocol: TCP
```

---

## Prod Overlay

**`k8s/overlays/prod/kustomization.yaml`**

```yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization

namespace: agentsmith

bases:
  - ../../base

resources:
  - ingress.yaml

patches:
  - path: patch-dispatcher-prod.yaml
    target:
      kind: Deployment
      name: agentsmith-dispatcher
```

**`k8s/overlays/prod/patch-dispatcher-prod.yaml`**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
spec:
  replicas: 2
  template:
    spec:
      containers:
        - name: dispatcher
          imagePullPolicy: Always
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: IMAGE_PULL_POLICY
              value: "Always"
          resources:
            requests:
              cpu: "200m"
              memory: "384Mi"
            limits:
              cpu: "1000m"
              memory: "768Mi"
```

**`k8s/overlays/prod/ingress.yaml`**

Replace `agentsmith.yourdomain.com` with your actual hostname.
Requires an Ingress controller (e.g. nginx-ingress) and cert-manager for TLS.

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: agentsmith-dispatcher
  namespace: agentsmith
  annotations:
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - agentsmith.yourdomain.com
      secretName: agentsmith-tls
  rules:
    - host: agentsmith.yourdomain.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: agentsmith-dispatcher
                port:
                  name: http
```

---

## Deployment Commands

### Dev (Docker Desktop / kind)

```bash
# Build images locally first
docker build -t agentsmith:latest .
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .

# Create namespace + all resources
kubectl apply -k k8s/overlays/dev

# Check status
kubectl get pods -n agentsmith
kubectl get svc -n agentsmith

# Access dispatcher directly (NodePort)
curl http://localhost:30080/health

# Or use port-forward as alternative
kubectl port-forward svc/agentsmith-dispatcher 8080:80 -n agentsmith
```

### Prod

```bash
# Tag and push images to your registry
docker build -t registry.yourdomain.com/agentsmith:v1.0.0 .
docker build -f Dockerfile.dispatcher -t registry.yourdomain.com/agentsmith-dispatcher:v1.0.0 .
docker push registry.yourdomain.com/agentsmith:v1.0.0
docker push registry.yourdomain.com/agentsmith-dispatcher:v1.0.0

# Update image references in prod overlay (or use kustomize edit set image)
kustomize edit set image agentsmith=registry.yourdomain.com/agentsmith:v1.0.0

# Apply
kubectl apply -k k8s/overlays/prod

# Check rollout
kubectl rollout status deployment/agentsmith-dispatcher -n agentsmith
```

---

## Secret Management

Secrets are **never** included in Kustomize overlays. Always apply them separately:

```bash
# From template (fill in values first)
cp k8s/secret-template.yaml k8s/secret-local.yaml
# Edit k8s/secret-local.yaml
kubectl apply -f k8s/secret-local.yaml
```

`k8s/secret-local.yaml` is in `.gitignore` and must never be committed.

For production, use one of:
- **Sealed Secrets**: `kubeseal` encrypts the secret for safe git storage
- **External Secrets Operator**: pulls secrets from AWS Secrets Manager / Vault / GCP SM
- **HashiCorp Vault**: agent sidecar injects secrets at pod startup

---

## ConfigMap from Local Config

The `agentsmith-config` ConfigMap is checked in with a placeholder template.
In practice, regenerate it from your local config before deploying:

```bash
kubectl create configmap agentsmith-config \
  --from-file=agentsmith.yml=config/agentsmith.yml \
  -n agentsmith \
  --dry-run=client -o yaml > k8s/base/configmap/agentsmith-config.yaml
```

---

## Definition of Done

- [ ] `kubectl apply -k k8s/overlays/dev` succeeds without errors
- [ ] `kubectl get pods -n agentsmith` shows redis + dispatcher Running
- [ ] `curl http://localhost:30080/health` returns `{"status":"ok"}`
- [ ] Dev overlay uses `imagePullPolicy: Never`
- [ ] Prod overlay uses `imagePullPolicy: Always`, 2 replicas, Ingress
- [ ] No secrets are embedded in any Kustomize file
- [ ] `kubectl apply -k k8s/overlays/prod` renders valid YAML (dry-run)

---

# Phase 19 – Step 8: Local K8s Test Guide

> **See also:** [`docs/docker-k8s-troubleshooting.md`](../../docs/docker-k8s-troubleshooting.md)
> for a full list of issues encountered during real local setup, including:
> - Redis connection string format (`host:port`, not `redis://`)
> - KubernetesClient 13.x type initializer bug (upgrade to 16.x)
> - `host.docker.internal` not resolvable in Linux containers
> - kubeconfig `127.0.0.1` must be replaced with `host.docker.internal`
> - `AzureDevOpsTicketProvider.ListOpenAsync` not implemented by default


## Goal

Verify the complete Agent Smith stack running on a local Kubernetes cluster
(Docker Desktop or kind) end-to-end: Redis, Dispatcher, agent Job spawning,
and Slack integration via ngrok.

---

## Prerequisites

| Tool | Install |
|------|---------|
| Docker Desktop (with K8s enabled) | https://www.docker.com/products/docker-desktop |
| `kubectl` | `brew install kubectl` |
| `kustomize` | `brew install kustomize` |
| `ngrok` | `brew install ngrok` |

> **Important:** Enable Kubernetes **before** starting the dispatcher.
> If you start the dispatcher first and then enable K8s, you must rebuild:
> `docker compose up -d --build dispatcher`

### Enable Kubernetes in Docker Desktop

1. Open Docker Desktop → **Settings → Kubernetes**
2. Check **Enable Kubernetes**
3. Click **Apply & Restart**
4. Wait until the K8s indicator in the bottom-left is green

Alternatively, use **kind**:

```bash
brew install kind
kind create cluster --name agentsmith
kubectl cluster-info --context kind-agentsmith
```

---

## Step 1: Build Both Images

```bash
cd agent-smith

# Agent container (used by K8s Jobs)
docker build -t agentsmith:latest .

# Dispatcher (long-running web service)
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .

# Verify
docker images | grep agentsmith
```

Both images must be present locally. The dev overlay uses `imagePullPolicy: Never`
so K8s will not try to pull from a registry.

> **Note:** After rebuilding images, always restart the dispatcher too:
> `docker compose up -d --build dispatcher`
> The dispatcher embeds the kubeconfig at startup — it does not reload it.

For **kind**, load images into the cluster:

```bash
kind load docker-image agentsmith:latest --name agentsmith
kind load docker-image agentsmith-dispatcher:latest --name agentsmith
```

---

## Step 2: Create the Namespace

```bash
kubectl apply -f k8s/base/namespace.yaml

# Verify
kubectl get namespace agentsmith
```

---

## Step 3: Create the Secret

```bash
# Copy the template
cp k8s/secret-template.yaml k8s/secret-local.yaml
```

Edit `k8s/secret-local.yaml` and fill in your real values:

```yaml
stringData:
  anthropic-api-key: "sk-ant-..."
  github-token: "ghp_..."
  slack-bot-token: "xoxb-..."
  slack-signing-secret: "your-signing-secret"
  redis-url: "redis://redis:6379"
  # leave others empty if not needed
```

Apply the secret:

```bash
kubectl apply -f k8s/secret-local.yaml

# Verify (values are base64-encoded, not shown in plain text)
kubectl get secret agentsmith-secrets -n agentsmith
```

---

## Step 4: Deploy the Stack

```bash
kubectl apply -k k8s/overlays/dev
```

This creates:
- `Namespace: agentsmith`
- `Deployment: redis`
- `Service: redis` (ClusterIP, port 6379)
- `Deployment: agentsmith-dispatcher`
- `Service: agentsmith-dispatcher` (NodePort 30080)
- `ServiceAccount: agentsmith`
- `Role: agentsmith-job-manager`
- `RoleBinding: agentsmith-job-manager`
- `ConfigMap: agentsmith-config`

---

## Step 5: Verify Everything Is Running

```bash
# Watch pods come up
kubectl get pods -n agentsmith -w

# Expected output (after ~30s):
# NAME                                     READY   STATUS    RESTARTS
# agentsmith-dispatcher-xxx-yyy            1/1     Running   0
# redis-xxx-yyy                            1/1     Running   0
```

Check services:

```bash
kubectl get svc -n agentsmith
# NAME                     TYPE        CLUSTER-IP     EXTERNAL-IP   PORT(S)
# agentsmith-dispatcher    NodePort    10.x.x.x       <none>        80:30080/TCP
# redis                    ClusterIP   10.x.x.x       <none>        6379/TCP
```

---

## Step 6: Test the Health Endpoint

The dev overlay exposes the dispatcher on `NodePort 30080`.

```bash
curl http://localhost:30080/health
# Expected: {"status":"ok","timestamp":"2026-..."}
```

If NodePort doesn't work (some Docker Desktop versions), use port-forward:

```bash
kubectl port-forward svc/agentsmith-dispatcher 8080:80 -n agentsmith &
curl http://localhost:8080/health
```

---

## Step 7: Expose to Slack via ngrok

Slack needs a public HTTPS URL to send events to. ngrok provides this in development.

```bash
# Expose NodePort 30080
ngrok http 30080
```

ngrok prints something like:

```
Forwarding  https://abc123.ngrok.io -> http://localhost:30080
```

Copy the `https://abc123.ngrok.io` URL.

---

## Step 8: Configure Slack App URLs

In your Slack App settings (https://api.slack.com/apps):

**Event Subscriptions:**
- Request URL: `https://abc123.ngrok.io/slack/events`
- Slack sends a URL verification challenge immediately — the Dispatcher handles it automatically

**Interactivity & Shortcuts:**
- Request URL: `https://abc123.ngrok.io/slack/interact`

Click **Save Changes** on both pages.

---

## Step 9: Test Slack Integration

In the Slack channel where you invited `@Agent Smith`:

### List Tickets
```
list tickets in my-project
```

Expected response within a few seconds:
```
🎫 Open tickets in my-project (N total):
• #1 — First ticket [New]
• #2 — Second ticket [Active]
```

### Fix a Ticket
```
fix #1 in my-project
```

Expected flow:
1. Bot: `:rocket: Starting Agent Smith for ticket #1 in my-project...`
2. Progress updates appear: `:gear: [1/9] FetchTicketCommand`
3. If agent has a question: Yes / No buttons appear
4. On completion: `:rocket: Done! ... :link: View Pull Request`

### Watch the K8s Job

In a separate terminal while the fix is running:

```bash
# See the job appear
kubectl get jobs -n agentsmith -w

# Watch the agent pod logs
kubectl logs -f -l app=agentsmith -n agentsmith
```

---

## Step 10: Verify Job Cleanup

After the job completes, it is automatically deleted after 5 minutes (TTL 300s):

```bash
# Immediately after completion
kubectl get jobs -n agentsmith
# NAME                    COMPLETIONS   DURATION
# agentsmith-a3f8c1d9    1/1           2m34s

# After 5 minutes
kubectl get jobs -n agentsmith
# No resources found in agentsmith namespace.
```

---

## Known Issues (already fixed in codebase)

| Issue | Symptom | Fix applied |
|-------|---------|-------------|
| Redis URI format | `RedisConnectionException` on startup | `REDIS_URL=redis:6379` (no `redis://`) |
| KubernetesClient 13.x bug | `KubernetesYaml type initializer` exception | Upgraded to `KubernetesClient 16.0.1` |
| kubeconfig server URL | `127.0.0.1:6443` unreachable from container | `Program.cs` replaces with `host.docker.internal` |
| `host.docker.internal` DNS | `Name or service not known` | `extra_hosts: host.docker.internal:host-gateway` in docker-compose |
| Azure DevOps `ListOpenAsync` | Always returns empty list | Implemented WIQL query in `AzureDevOpsTicketProvider` |
| `fix ticket #N` not parsed | `❓ I didn't understand that` | Regex updated: `fix\s+(?:ticket\s+)?#(\d+)` |

---

## Troubleshooting

### Pod stuck in `Pending`

```bash
kubectl describe pod -n agentsmith -l app=agentsmith-dispatcher
```

Common causes:
- Image not found: make sure `docker build` completed successfully
- Secret missing: check `kubectl get secret agentsmith-secrets -n agentsmith`

### Dispatcher CrashLoopBackOff

```bash
kubectl logs -n agentsmith -l app=agentsmith-dispatcher --previous
```

Common causes:
- Redis not ready yet (wait a few seconds and retry)
- `SLACK_BOT_TOKEN` empty in the secret

### Slack URL verification fails

- Make sure ngrok is running and the URL is correct
- Check Dispatcher logs: `kubectl logs -n agentsmith -l app=agentsmith-dispatcher -f`
- Look for the incoming `url_verification` request in the logs

### Agent Job fails immediately

```bash
kubectl logs -n agentsmith -l app=agentsmith --previous
```

Common causes:
- `ANTHROPIC_API_KEY` or `GITHUB_TOKEN` not set in the secret
- `config/agentsmith.yml` project name doesn't match the Slack command

### Redis connection refused

```bash
# Test Redis connectivity from inside the cluster
kubectl run redis-test --rm -it --image=redis:7-alpine -n agentsmith -- \
  redis-cli -h redis ping
# Expected: PONG
```

---

## Teardown

```bash
# Remove everything in the agentsmith namespace
kubectl delete namespace agentsmith

# Or just the deployments (keep namespace + secret)
kubectl delete -k k8s/overlays/dev
```

---

### Correct Startup Order

Follow this order to avoid the most common issues:

```bash
# 1. Enable K8s in Docker Desktop and wait for: kubectl get nodes → Ready
# 2. Fill in .env (ANTHROPIC_API_KEY, SLACK_BOT_TOKEN, SLACK_SIGNING_SECRET, ...)
# 3. docker compose up -d redis dispatcher
# 4. curl http://localhost:6000/health  → {"status":"ok"}
# 5. docker compose logs dispatcher | grep -E "INFO|WARN"
#    → [INFO] Kubernetes available — 'fix' commands enabled.
# 6. ngrok http 6000
# 7. Update Slack Event Subscriptions + Interactivity URLs with new ngrok URL
# 8. /invite @Agent Smith in your Slack channel
# 9. list tickets in <project>   →  tickets appear
#    fix #N in <project>         →  job spawns, progress in Slack
```

---

## Success Criteria

- [ ] `kubectl get pods -n agentsmith` shows redis + dispatcher `Running`
- [ ] `curl http://localhost:30080/health` returns `{"status":"ok"}`
- [ ] ngrok URL passes Slack's URL verification challenge
- [ ] `list tickets in my-project` returns ticket list in Slack
- [ ] `fix #N in my-project` spawns a K8s Job (visible in `kubectl get jobs`)
- [ ] Progress messages appear in Slack in real time
- [ ] Job pod is cleaned up after TTL (5 minutes post-completion)
- [ ] `kubectl apply -k k8s/overlays/prod --dry-run=client` renders valid YAML

---

# Phase 19 – Step 6: RBAC (ServiceAccount + Role + RoleBinding)

## Goal

Grant the Dispatcher pod the minimum Kubernetes permissions it needs to
spawn and monitor agent Jobs in the `agentsmith` namespace.
Cluster-wide permissions are NOT required — a namespace-scoped `Role` is sufficient.

---

## Files

```
k8s/base/rbac/
├── serviceaccount.yaml
├── role.yaml
└── rolebinding.yaml
```

---

## serviceaccount.yaml

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: agentsmith
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
```

The Dispatcher Deployment references this ServiceAccount via
`spec.template.spec.serviceAccountName: agentsmith`.

---

## role.yaml

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: agentsmith-job-manager
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
rules:
  - apiGroups: ["batch"]
    resources: ["jobs"]
    verbs: ["create", "delete", "get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods"]
    verbs: ["get", "list", "watch"]
  - apiGroups: [""]
    resources: ["pods/log"]
    verbs: ["get"]
```

### Why these permissions?

| Permission | Reason |
|-----------|--------|
| `batch/jobs: create` | `JobSpawner.SpawnAsync()` calls `BatchV1.CreateNamespacedJobAsync()` |
| `batch/jobs: delete` | Future: allow cancelling a running job from Slack |
| `batch/jobs: get/list/watch` | K8s client needs these for internal resource version tracking |
| `pods: get/list/watch` | Read pod status to determine if job container is running |
| `pods/log: get` | Optional: surface agent container logs in Slack on error |

No access to Secrets, ConfigMaps, Deployments or any other resource type.

---

## rolebinding.yaml

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: agentsmith-job-manager
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: rbac
subjects:
  - kind: ServiceAccount
    name: agentsmith
    namespace: agentsmith
roleRef:
  kind: Role
  name: agentsmith-job-manager
  apiGroup: rbac.authorization.k8s.io
```

Binds the `agentsmith-job-manager` Role to the `agentsmith` ServiceAccount
in the same namespace.

---

## How the Dispatcher Uses the ServiceAccount

The K8s client in the Dispatcher resolves credentials automatically when
running inside a cluster:

```csharp
var k8sConfig = KubernetesClientConfiguration.IsInCluster()
    ? KubernetesClientConfiguration.InClusterConfig()   // uses mounted ServiceAccount token
    : KubernetesClientConfiguration.BuildConfigFromConfigFile(); // uses ~/.kube/config (local dev)
```

When running in-cluster, Kubernetes mounts a token at
`/var/run/secrets/kubernetes.io/serviceaccount/token` automatically.
The `KubernetesClient` library picks this up via `InClusterConfig()`.

---

## Kustomize Integration

All three files are listed in `k8s/base/kustomization.yaml`:

```yaml
resources:
  - rbac/serviceaccount.yaml
  - rbac/role.yaml
  - rbac/rolebinding.yaml
```

Kustomize automatically applies the `namespace: agentsmith` override
from the base `kustomization.yaml`, so the namespace field is set
consistently across all resources.

---

## Security Notes

- The Role is **namespace-scoped** (`Role`, not `ClusterRole`).
  The Dispatcher cannot touch resources in any other namespace.
- The agent Job pods themselves run with the **default** ServiceAccount,
  which has no special permissions — they only need outbound network access to:
  - Redis (in-cluster)
  - AI provider APIs (external)
  - Source control APIs (external)
- There is no `secrets: get` permission. Secrets are injected as environment
  variables by the Job spec — the Dispatcher never reads secret values directly.

---

## Verification

```bash
# Apply and verify
kubectl apply -k k8s/overlays/dev

# Check ServiceAccount exists
kubectl get serviceaccount agentsmith -n agentsmith

# Check Role exists
kubectl get role agentsmith-job-manager -n agentsmith -o yaml

# Check RoleBinding
kubectl get rolebinding agentsmith-job-manager -n agentsmith

# Test: can the dispatcher ServiceAccount create jobs?
kubectl auth can-i create jobs \
  --as=system:serviceaccount:agentsmith:agentsmith \
  -n agentsmith
# Expected: yes

# Test: can it access secrets? (should be no)
kubectl auth can-i get secrets \
  --as=system:serviceaccount:agentsmith:agentsmith \
  -n agentsmith
# Expected: no
```

---

## Definition of Done

- [ ] `serviceaccount.yaml` creates `agentsmith` ServiceAccount in `agentsmith` namespace
- [ ] `role.yaml` grants `batch/jobs` CRUD + `pods` read in `agentsmith` namespace
- [ ] `rolebinding.yaml` binds the Role to the ServiceAccount
- [ ] Dispatcher Deployment references `serviceAccountName: agentsmith`
- [ ] `kubectl auth can-i create jobs` returns `yes` for the ServiceAccount
- [ ] `kubectl auth can-i get secrets` returns `no` for the ServiceAccount
- [ ] `kubectl apply -k k8s/overlays/dev` applies all three RBAC resources cleanly

---

# Phase 19 – Step 2: Redis Deployment + Service

## Goal

Deploy Redis as an ephemeral in-memory message bus inside the `agentsmith` namespace.
No PersistentVolume — messages are short-lived (job lifetime = minutes).
If Redis restarts, in-flight jobs are considered lost (acceptable tradeoff).

---

## Files

- `k8s/base/redis/deployment.yaml`
- `k8s/base/redis/service.yaml`

---

## Redis Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: agentsmith
  labels:
    app: redis
    app.kubernetes.io/name: redis
    app.kubernetes.io/component: message-bus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          args:
            - "--maxmemory"
            - "256mb"
            - "--maxmemory-policy"
            - "allkeys-lru"
            - "--save"
            - ""        # disable RDB persistence entirely
          ports:
            - name: redis
              containerPort: 6379
              protocol: TCP
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "500m"
              memory: "384Mi"
          livenessProbe:
            exec:
              command: ["redis-cli", "ping"]
            initialDelaySeconds: 10
            periodSeconds: 15
            timeoutSeconds: 3
          readinessProbe:
            exec:
              command: ["redis-cli", "ping"]
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 3
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: false
            runAsNonRoot: true
            runAsUser: 999   # redis user in the official image
```

---

## Redis Service

ClusterIP service — Redis is internal only. Never exposed outside the cluster.

```yaml
apiVersion: v1
kind: Service
metadata:
  name: redis
  namespace: agentsmith
  labels:
    app: redis
    app.kubernetes.io/name: redis
    app.kubernetes.io/component: message-bus
spec:
  type: ClusterIP
  selector:
    app: redis
  ports:
    - name: redis
      port: 6379
      targetPort: 6379
      protocol: TCP
```

The in-cluster DNS name is: `redis.agentsmith.svc.cluster.local:6379`
Short form (same namespace): `redis:6379`

The `redis-url` secret value should be: `redis://redis:6379`

---

## Design Decisions

### No PersistentVolume

Redis runs in pure in-memory mode (`--save ""`). Reasons:
- All messages are ephemeral — job lifetime is measured in minutes
- Stream TTL is 2 hours; streams are explicitly deleted on job completion
- Adding a PV later requires only one config change (`--appendonly yes` + PVC)
- Simpler deployment, no storage class dependency

### maxmemory-policy: allkeys-lru

If memory pressure occurs (unlikely at 256mb for small workloads),
LRU eviction removes the least-recently-used keys. This means old stream
entries are evicted before active ones.

### Single Replica

Redis does not support multi-replica writes without Redis Cluster or Sentinel.
For this workload (short-lived jobs, local channels) a single replica is correct.
HA upgrade path: Redis Cluster or an external managed Redis (Upstash, ElastiCache).

---

## Connection String

The Dispatcher and agent containers connect using:

```
REDIS_URL=redis://redis:6379
```

Stored in the `agentsmith-secrets` K8s Secret under key `redis-url`.

---

## Registering in Kustomize Base

Both files are listed in `k8s/base/kustomization.yaml`:

```yaml
resources:
  - namespace.yaml
  - redis/deployment.yaml
  - redis/service.yaml
  - dispatcher/deployment.yaml
  - dispatcher/service.yaml
  - rbac/serviceaccount.yaml
  - rbac/role.yaml
  - rbac/rolebinding.yaml
  - configmap/agentsmith-config.yaml
```

---

## Verification

```bash
# Apply base manifests
kubectl apply -k k8s/overlays/dev

# Check Redis pod is Running
kubectl get pods -n agentsmith -l app=redis

# Verify Redis is reachable from inside the cluster
kubectl run redis-test --rm -it --image=redis:7-alpine -n agentsmith -- \
  redis-cli -h redis ping
# Expected output: PONG
```

---

## Definition of Done

- [ ] `redis/deployment.yaml` applies without errors
- [ ] `redis/service.yaml` applies without errors
- [ ] Redis pod reaches `Running` state
- [ ] `redis-cli ping` returns `PONG` from inside the cluster
- [ ] No PersistentVolume or PersistentVolumeClaim referenced
- [ ] `--save ""` disables persistence
- [ ] Memory limit set to 384Mi (headroom above 256mb maxmemory)

---

# Phase 19 – Step 3: K8s Secret Schema + secret-template.yaml

## Goal

Define the Kubernetes Secret that holds all sensitive credentials for the Agent Smith
stack. Provide a committed template (with empty values) and document how to populate
it safely without ever committing real secrets.

---

## Files

| File | Committed? | Purpose |
|------|-----------|---------|
| `k8s/secret-template.yaml` | Yes (empty values) | Template for operators to copy and fill |
| `k8s/secret-local.yaml` | **Never** (in `.gitignore`) | Locally populated secret |

---

## Secret Manifest

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: agentsmith-secrets
  namespace: agentsmith
  labels:
    app.kubernetes.io/name: agentsmith
    app.kubernetes.io/component: secrets
type: Opaque
stringData:
  # --- AI Providers ---
  anthropic-api-key: ""          # sk-ant-api03-...
  openai-api-key: ""             # sk-...          (optional)
  gemini-api-key: ""             # AIza...         (optional)

  # --- Source Control & Ticket Systems ---
  github-token: ""               # ghp_...  (scopes: repo, issues)
  azure-devops-token: ""         # PAT (scopes: Code R/W, Work Items R/W)
  gitlab-token: ""               # glpat-... (scopes: api, write_repository)
  jira-token: ""                 # Jira API token (Cloud) or PAT (Server)
  jira-email: ""                 # Email associated with the Jira API token

  # --- Chat Platforms ---
  slack-bot-token: ""            # xoxb-...  (Bot User OAuth Token)
  slack-signing-secret: ""       # Found in: Basic Information → App Credentials

  # --- Infrastructure ---
  redis-url: "redis://redis:6379"  # Redis connection string inside the cluster
```

---

## How to Use

### Local / Dev

```bash
# Copy the template
cp k8s/secret-template.yaml k8s/secret-local.yaml

# Edit with your real values
# (use your editor of choice)

# Apply to the cluster
kubectl apply -f k8s/secret-local.yaml
```

`k8s/secret-local.yaml` must be listed in `.gitignore`:

```gitignore
k8s/secret-local.yaml
```

### One-liner (no yaml file needed)

```bash
kubectl create secret generic agentsmith-secrets \
  --namespace agentsmith \
  --from-literal=anthropic-api-key="sk-ant-..." \
  --from-literal=github-token="ghp_..." \
  --from-literal=slack-bot-token="xoxb-..." \
  --from-literal=slack-signing-secret="..." \
  --from-literal=redis-url="redis://redis:6379" \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Production: Sealed Secrets

For production clusters, never store plaintext secrets in version control.
Use one of:

- **Sealed Secrets** (Bitnami): `kubeseal` encrypts the secret with the cluster's public key
- **External Secrets Operator**: pulls from HashiCorp Vault, AWS Secrets Manager, Azure Key Vault, etc.
- **SOPS + age/GPG**: encrypts the secret file before committing

---

## How the Secret is Mounted

The Dispatcher Deployment reads credentials via `secretKeyRef`:

```yaml
env:
  - name: SLACK_BOT_TOKEN
    valueFrom:
      secretKeyRef:
        name: agentsmith-secrets
        key: slack-bot-token
  - name: SLACK_SIGNING_SECRET
    valueFrom:
      secretKeyRef:
        name: agentsmith-secrets
        key: slack-signing-secret
  - name: REDIS_URL
    valueFrom:
      secretKeyRef:
        name: agentsmith-secrets
        key: redis-url
```

The K8s Job containers (spawned by `JobSpawner`) read AI provider and SCM tokens
using the same pattern with `optional: true` so missing keys don't crash the pod:

```yaml
env:
  - name: ANTHROPIC_API_KEY
    valueFrom:
      secretKeyRef:
        name: agentsmith-secrets
        key: anthropic-api-key
        optional: true
```

---

## Secret Key Reference

| Key | Environment Variable | Consumer |
|-----|---------------------|---------|
| `anthropic-api-key` | `ANTHROPIC_API_KEY` | Agent container |
| `openai-api-key` | `OPENAI_API_KEY` | Agent container |
| `gemini-api-key` | `GEMINI_API_KEY` | Agent container |
| `github-token` | `GITHUB_TOKEN` | Agent container |
| `azure-devops-token` | `AZURE_DEVOPS_TOKEN` | Agent container |
| `gitlab-token` | `GITLAB_TOKEN` | Agent container |
| `jira-token` | `JIRA_TOKEN` | Agent container |
| `jira-email` | `JIRA_EMAIL` | Agent container |
| `slack-bot-token` | `SLACK_BOT_TOKEN` | Dispatcher |
| `slack-signing-secret` | `SLACK_SIGNING_SECRET` | Dispatcher |
| `redis-url` | `REDIS_URL` | Dispatcher + Agent container |

---

## .gitignore Entry

Ensure this is present in the root `.gitignore`:

```gitignore
# Local K8s secrets - never commit
k8s/secret-local.yaml
```

---

## Definition of Done

- [ ] `k8s/secret-template.yaml` committed with all keys and empty string values
- [ ] All 12 keys documented with their purpose
- [ ] `k8s/secret-local.yaml` in `.gitignore`
- [ ] `kubectl apply -f k8s/secret-local.yaml` works after filling in values
- [ ] Dispatcher Deployment references the correct key names
- [ ] Job containers reference keys with `optional: true`
