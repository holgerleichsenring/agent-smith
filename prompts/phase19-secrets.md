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
