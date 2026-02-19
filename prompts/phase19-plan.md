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
COPY src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj   src/AgentSmith.Dispatcher/
RUN dotnet restore src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj
COPY src/ src/
RUN dotnet publish src/AgentSmith.Dispatcher -c Release -o /app/publish

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
ENTRYPOINT ["dotnet", "AgentSmith.Dispatcher.dll"]
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