# Kubernetes

Production deployment with Kustomize base and overlays for dev/prod environments. The Dispatcher spawns ephemeral Jobs per request, with Redis as the message bus.

## Architecture

```
┌─────────────────────────────────────────────────┐
│ Kubernetes Cluster                              │
│                                                 │
│  ┌──────────────┐     ┌──────────────┐         │
│  │  Dispatcher   │────▶│    Redis     │         │
│  │  (Deployment) │     │  (StatefulSet)│        │
│  └──────┬───────┘     └──────────────┘         │
│         │                     ▲                  │
│         │ spawns              │ progress         │
│         ▼                     │                  │
│  ┌──────────────┐    ┌──────────────┐          │
│  │  Agent Job 1  │    │  Agent Job 2  │         │
│  │  (ephemeral)  │───▶│  (ephemeral)  │        │
│  └──────────────┘    └──────────────┘          │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Kustomize Structure

The `k8s/` directory provides a base with dev and prod overlays:

```
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── configmap/
│   ├── dispatcher/
│   ├── rbac/
│   └── redis/
├── overlays/
│   ├── dev/
│   └── prod/
└── secret-template.yaml
```

## Quick Start

### 1. Create the Namespace

```bash
kubectl apply -f k8s/base/namespace.yaml
```

### 2. Create Secrets

Copy the template and fill in your values:

```bash
cp k8s/secret-template.yaml k8s/my-secrets.yaml
```

```yaml
# k8s/my-secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: agentsmith-secrets
  namespace: agentsmith
type: Opaque
stringData:
  ANTHROPIC_API_KEY: "sk-ant-..."
  GITHUB_TOKEN: "ghp_..."
  SLACK_BOT_TOKEN: "xoxb-..."
  SLACK_SIGNING_SECRET: "..."
```

```bash
kubectl apply -f k8s/my-secrets.yaml
```

!!! warning "Do not commit secrets"
    The `secret-template.yaml` is a template only. Never commit actual secrets to Git. Consider using Sealed Secrets, SOPS, or an external secret store.

### 3. Deploy with Kustomize

**Development:**

```bash
kubectl apply -k k8s/overlays/dev
```

**Production:**

```bash
kubectl apply -k k8s/overlays/prod
```

## How the Dispatcher Spawns Jobs

When a user sends a command via Slack (e.g., "fix #42 in my-api"), the Dispatcher:

1. Parses the intent (regex + LLM fallback)
2. Resolves the project from the configuration
3. Creates a Kubernetes Job with the appropriate command
4. The Job runs the `holgerleichsenring/agent-smith:latest` image
5. Progress streams back via Redis pub/sub
6. The Dispatcher relays updates to the Slack channel
7. The Job terminates when the pipeline completes

```yaml
# Example spawned Job (simplified)
apiVersion: batch/v1
kind: Job
metadata:
  name: agentsmith-fix-42-abc123
  namespace: agentsmith
spec:
  backoffLimit: 0
  ttlSecondsAfterFinished: 3600
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: agent
          image: holgerleichsenring/agent-smith:latest
          command: ["fix", "--repo", "https://github.com/org/my-api", "--ticket", "42", "--headless"]
          envFrom:
            - secretRef:
                name: agentsmith-secrets
```

## RBAC

The Dispatcher needs permissions to create and watch Jobs in its namespace:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: agentsmith-job-spawner
  namespace: agentsmith
rules:
  - apiGroups: ["batch"]
    resources: ["jobs"]
    verbs: ["create", "get", "list", "watch", "delete"]
  - apiGroups: [""]
    resources: ["pods", "pods/log"]
    verbs: ["get", "list", "watch"]
```

This is included in `k8s/base/rbac/`.

## Key Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `REDIS_URL` | Redis connection string | `redis:6379` |
| `SPAWNER_TYPE` | `kubernetes` or `docker` | `kubernetes` |
| `K8S_NAMESPACE` | Namespace for spawned Jobs | `agentsmith` |
| `AGENTSMITH_IMAGE` | Image for spawned agent Jobs | `holgerleichsenring/agent-smith:latest` |
| `IMAGE_PULL_POLICY` | K8s image pull policy | `IfNotPresent` |
| `K8S_SECRET_NAME` | Secret to mount in Jobs | `agentsmith-secrets` |

## Scaling

- The **Dispatcher** is stateless and can be scaled horizontally.
- **Redis** runs as a single instance (256 MB, no persistence). For HA, consider Redis Sentinel or a managed Redis service.
- **Agent Jobs** are ephemeral. Kubernetes handles scheduling and resource limits.

## Monitoring

Check the Dispatcher health endpoint:

```bash
kubectl port-forward svc/dispatcher 8081:8081 -n agentsmith
curl http://localhost:8081/health
```

List active agent jobs:

```bash
kubectl get jobs -n agentsmith -l app=agentsmith
```

View agent logs:

```bash
kubectl logs -n agentsmith job/agentsmith-fix-42-abc123
```
