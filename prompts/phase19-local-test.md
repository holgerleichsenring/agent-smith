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