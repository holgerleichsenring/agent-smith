# Phase 19b: Kubernetes — Fully Self-Contained with Helm

## Goal

Make the Kubernetes deployment fully self-contained and production-ready.
Replace the manual `apply-k8s-secret.sh` script with a proper Helm Chart.
No Docker Compose involved. No manual kubectl commands required for standard deployments.

---

## The Problem Today

```
k8s/ contains Kustomize manifests — good structure, but:
- Secrets are managed via a custom shell script (apply-k8s-secret.sh)
- No templating for image tags, replica counts, resource limits
- No standard upgrade/rollback story
- ConfigMap must be manually regenerated from agentsmith.yml
```

---

## The Solution: Helm Chart

A single `helm install` or `helm upgrade` brings up the full stack:

```bash
# First install
helm install agentsmith ./helm/agentsmith \
  --namespace agentsmith --create-namespace \
  -f helm/agentsmith/values.local.yaml

# Upgrade after code change
helm upgrade agentsmith ./helm/agentsmith \
  --namespace agentsmith \
  -f helm/agentsmith/values.local.yaml

# Uninstall
helm uninstall agentsmith --namespace agentsmith
```

---

## Chart Structure

```
helm/
└── agentsmith/
    ├── Chart.yaml                    # Chart metadata
    ├── values.yaml                   # Default values (no secrets)
    ├── values.local.yaml             # Local overrides (gitignored)
    ├── values.prod.yaml              # Production overrides (gitignored)
    └── templates/
        ├── _helpers.tpl              # Common labels, selectors
        ├── namespace.yaml            # Namespace (optional)
        ├── secret.yaml               # K8s Secret from Helm values
        ├── configmap.yaml            # agentsmith.yml as ConfigMap
        ├── redis/
        │   ├── deployment.yaml
        │   └── service.yaml
        ├── dispatcher/
        │   ├── deployment.yaml
        │   └── service.yaml
        ├── rbac/
        │   ├── serviceaccount.yaml
        │   ├── role.yaml
        │   └── rolebinding.yaml
        └── ingress.yaml              # Optional, enabled via values
```

---

## values.yaml (defaults, no secrets)

```yaml
# Image configuration
image:
  agent: agentsmith:latest
  dispatcher: agentsmith-dispatcher:latest
  pullPolicy: IfNotPresent       # use Always in prod

# Spawner
spawner:
  type: kubernetes               # kubernetes | docker
  namespace: agentsmith
  secretName: agentsmith-secrets
  ttlSecondsAfterFinished: 300
  jobResources:
    requests:
      cpu: "250m"
      memory: "512Mi"
    limits:
      cpu: "1000m"
      memory: "1Gi"

# Dispatcher
dispatcher:
  replicas: 1
  port: 8080
  resources:
    requests:
      cpu: "100m"
      memory: "128Mi"
    limits:
      cpu: "500m"
      memory: "512Mi"

# Redis
redis:
  image: redis:7-alpine
  maxMemory: "256mb"
  resources:
    requests:
      cpu: "100m"
      memory: "128Mi"
    limits:
      cpu: "500m"
      memory: "384Mi"

# Ingress (disabled by default)
ingress:
  enabled: false
  host: ""
  tls: false
  annotations: {}

# Secrets — leave empty here, override in values.local.yaml / values.prod.yaml
secrets:
  anthropicApiKey: ""
  openaiApiKey: ""
  geminiApiKey: ""
  githubToken: ""
  azureDevOpsToken: ""
  gitlabToken: ""
  jiraToken: ""
  jiraEmail: ""
  slackBotToken: ""
  slackSigningSecret: ""

# Config — inline agentsmith.yml content
# Override with: --set-file config.content=config/agentsmith.yml
config:
  content: ""
```

---

## values.local.yaml (gitignored, for Docker Desktop / kind)

```yaml
image:
  pullPolicy: Never              # use locally built images

dispatcher:
  replicas: 1

secrets:
  anthropicApiKey: "sk-ant-..."
  azureDevOpsToken: "..."
  slackBotToken: "xoxb-..."
  slackSigningSecret: "..."
```

---

## values.prod.yaml (gitignored, for production cluster)

```yaml
image:
  pullPolicy: Always

dispatcher:
  replicas: 2

ingress:
  enabled: true
  host: agentsmith.yourcompany.com
  tls: true
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod

# In prod: prefer External Secrets Operator or Sealed Secrets
# secrets: managed externally
```

---

## Secret Template

Helm renders the secret from values — no shell script needed:

```yaml
# templates/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: agentsmith-secrets
  namespace: {{ .Release.Namespace }}
type: Opaque
stringData:
  anthropic-api-key: {{ .Values.secrets.anthropicApiKey | quote }}
  openai-api-key: {{ .Values.secrets.openaiApiKey | quote }}
  gemini-api-key: {{ .Values.secrets.geminiApiKey | quote }}
  github-token: {{ .Values.secrets.githubToken | quote }}
  azure-devops-token: {{ .Values.secrets.azureDevOpsToken | quote }}
  gitlab-token: {{ .Values.secrets.gitlabToken | quote }}
  jira-token: {{ .Values.secrets.jiraToken | quote }}
  jira-email: {{ .Values.secrets.jiraEmail | quote }}
  slack-bot-token: {{ .Values.secrets.slackBotToken | quote }}
  slack-signing-secret: {{ .Values.secrets.slackSigningSecret | quote }}
  redis-url: "redis:6379"
```

---

## ConfigMap Template

```yaml
# templates/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agentsmith-config
  namespace: {{ .Release.Namespace }}
data:
  agentsmith.yml: |
    {{ .Values.config.content | nindent 4 }}
```

Usage:
```bash
helm upgrade agentsmith ./helm/agentsmith \
  --set-file config.content=config/agentsmith.yml \
  -f helm/agentsmith/values.local.yaml
```

---

## Kustomize vs Helm

The existing `k8s/` Kustomize structure is kept as-is for reference.
Helm is the recommended deployment method going forward.

| | Kustomize (`k8s/`) | Helm (`helm/agentsmith/`) |
|---|---|---|
| Secrets | `apply-k8s-secret.sh` | `values.local.yaml` |
| Config | Manual kubectl command | `--set-file` |
| Upgrades | `kubectl apply -k` | `helm upgrade` |
| Rollback | Manual | `helm rollback` |
| Status | `kubectl get pods` | `helm status agentsmith` |

---

## Phase 19b Steps

| Step | File | Description |
|------|------|-------------|
| 19b-1 | `phase19b-chart.md` | Chart.yaml, values.yaml, _helpers.tpl |
| 19b-2 | `phase19b-secret-configmap.md` | secret.yaml + configmap.yaml templates |
| 19b-3 | `phase19b-redis.md` | Redis Deployment + Service templates |
| 19b-4 | `phase19b-dispatcher.md` | Dispatcher Deployment + Service + Ingress templates |
| 19b-5 | `phase19b-rbac.md` | ServiceAccount + Role + RoleBinding templates |
| 19b-6 | `phase19b-guide.md` | Local test guide + prod deployment guide |

---

## Constraints & Notes

- `values.local.yaml` and `values.prod.yaml` are gitignored (contain secrets)
- For production: External Secrets Operator or Sealed Secrets is recommended
  over putting secrets in values files
- The `k8s/` Kustomize directory remains but is no longer the primary path
- Docker Compose is not involved in any K8s deployment step

---

## Success Criteria

- [ ] `helm install agentsmith ./helm/agentsmith -f values.local.yaml` deploys full stack
- [ ] `kubectl get pods -n agentsmith` shows redis + dispatcher Running
- [ ] `helm upgrade` with new image tag rolls out without downtime
- [ ] `helm uninstall` cleanly removes all resources
- [ ] `helm status agentsmith` shows meaningful deployment info
- [ ] No `apply-k8s-secret.sh` needed for K8s deployments
- [ ] `values.local.yaml` and `values.prod.yaml` are in `.gitignore`

---

## Dependencies

- Helm v3 installed locally
- Phase 19a complete (IJobSpawner interface exists)
- `agentsmith:latest` and `agentsmith-dispatcher:latest` images built
- For prod: a running K8s cluster with ingress controller (if ingress enabled)