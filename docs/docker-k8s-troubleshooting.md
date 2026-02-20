# Docker + Kubernetes Troubleshooting Guide

This document captures every issue encountered during a real local setup of
Agent Smith with Docker Compose + Docker Desktop Kubernetes. Each section
describes the symptom, root cause, and fix.

---

## Issue 1: Redis connection fails on startup

**Symptom**
```
Unhandled exception. StackExchange.Redis.RedisConnectionException:
It was not possible to connect to the redis server(s).
```

**Root Cause**

`StackExchange.Redis` does **not** understand `redis://` URI syntax.
It expects plain `host:port` format.

The docker-compose had:
```yaml
REDIS_URL=redis://redis:6379   # ❌ wrong
```

**Fix**
```yaml
REDIS_URL=redis:6379           # ✅ correct
```

---

## Issue 2: Dispatcher crashes — kubeconfig not found

**Symptom**
```
Unhandled exception. k8s.Exceptions.KubeConfigException:
kubeconfig file not found at /home/dispatcher/.kube/config
```

**Root Cause**

The dispatcher container tried to load `~/.kube/config` but it didn't
exist inside the container. Either:
- Kubernetes was not yet enabled in Docker Desktop, so `~/.kube/config`
  didn't exist on the host
- The volume mount `~/.kube:/home/dispatcher/.kube:ro` failed silently
  because the source directory didn't exist

**Fix**

1. Enable Kubernetes in Docker Desktop:
   **Settings → Kubernetes → Enable Kubernetes → Apply & Restart**

2. Wait until the indicator in Docker Desktop is green (~1-2 minutes)

3. Verify: `kubectl get nodes` should show `docker-desktop   Ready`

4. Rebuild and restart the dispatcher:
   ```bash
   docker compose up -d --build dispatcher
   ```

The dispatcher is designed to start without K8s — `list tickets` and
`create ticket` still work. Only `fix` requires K8s.

---

## Issue 3: KubernetesYaml type initializer exception

**Symptom**
```
[WARN] Kubernetes not available:
The type initializer for 'k8s.KubernetesYaml' threw an exception.
```

**Root Cause**

`KubernetesClient` version 13.x has a bug where the static constructor
of `KubernetesYaml` throws an exception when running inside a Linux Docker
container. The YAML serializer registration fails at runtime.

**Fix**

Upgrade `KubernetesClient` to version 16.x in
`src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj`:

```xml
<PackageReference Include="KubernetesClient" Version="16.0.1" />
```

---

## Issue 4: K8s API not reachable — host.docker.internal not resolved

**Symptom**

```
❌ Agent Smith encountered an error:
Name or service not known (host.docker.internal:6443)
```

**Root Cause**

Two problems stacked on top of each other:

1. The kubeconfig points to `https://127.0.0.1:6443` (Docker Desktop's
   K8s API address on the host). From inside a Docker container, `127.0.0.1`
   refers to the container itself — not the host. The fix is to replace it
   with `host.docker.internal`.

2. `host.docker.internal` is **not** automatically resolvable inside Linux
   Docker containers. On macOS/Windows the Docker Desktop daemon injects it,
   but inside the container's DNS it is not available unless explicitly mapped.

**Fix — Part 1: Replace 127.0.0.1 in kubeconfig**

In `Program.cs`, after reading the kubeconfig file, replace the server URL
before building the K8s client:

```csharp
var kubeConfigYaml = await File.ReadAllTextAsync(kubeConfigPath);
kubeConfigYaml = kubeConfigYaml.Replace(
    "https://127.0.0.1:", "https://host.docker.internal:");

using var stream = new MemoryStream(Encoding.UTF8.GetBytes(kubeConfigYaml));
k8sConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
```

**Fix — Part 2: Add extra_hosts to docker-compose**

```yaml
dispatcher:
  extra_hosts:
    - "host.docker.internal:host-gateway"
```

`host-gateway` is a Docker special value that resolves to the host machine's
gateway IP (typically `172.17.0.1` on Linux / the host on macOS).
This makes `host.docker.internal` resolvable from inside the container.

---

## Issue 5: Missing environment variables cause noisy warnings

**Symptom**

Docker Compose prints warnings for every undefined variable:
```
WARN[0000] The "GITHUB_TOKEN" variable is not set. Defaulting to a blank string.
WARN[0000] The "OPENAI_API_KEY" variable is not set. Defaulting to a blank string.
```

This makes it look like important variables are missing, even when only
optional ones (like `OPENAI_API_KEY`) are absent.

**Fix**

Use `${VAR:-}` syntax (empty default) for all optional variables in
`docker-compose.yml`:

```yaml
environment:
  - GITHUB_TOKEN=${GITHUB_TOKEN:-}
  - OPENAI_API_KEY=${OPENAI_API_KEY:-}
  - GEMINI_API_KEY=${GEMINI_API_KEY:-}
```

Required variables (`ANTHROPIC_API_KEY`, `SLACK_BOT_TOKEN`, etc.) should
still be set explicitly in `.env`.

---

## Issue 6: Fix command not recognized

**Symptom**

User types `fix ticket #55 in agent-smith-test` and the bot responds:
```
❓ I didn't understand that. Try:
• fix #65 in todo-list
```

**Root Cause**

The `ChatIntentParser` regex was:
```
^fix\s+#(\d+)\s+in\s+(\S+)$
```

This only matches `fix #55 in project`, not `fix ticket #55 in project`.

**Fix**

Make the word `ticket` optional:
```
^fix\s+(?:ticket\s+)?#(\d+)\s+in\s+(\S+)$
```

Now both variants work:
- `fix #55 in agent-smith-test` ✅
- `fix ticket #55 in agent-smith-test` ✅

---

## Issue 7: list tickets returns empty despite tickets existing

**Symptom**

```
✅ No open tickets found in agent-smith-test.
```

But Azure DevOps clearly shows open work items.

**Root Cause**

`AzureDevOpsTicketProvider` did not implement `ListOpenAsync`. The interface
`ITicketProvider` has a default implementation that always returns an empty list:

```csharp
Task<IReadOnlyList<Ticket>> ListOpenAsync(...) =>
    Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());
```

**Fix**

Implement `ListOpenAsync` in `AzureDevOpsTicketProvider` using a WIQL query:

```csharp
public async Task<IReadOnlyList<Ticket>> ListOpenAsync(
    CancellationToken cancellationToken = default)
{
    var client = CreateClient();
    var wiql = new Wiql
    {
        Query = $"""
            SELECT [System.Id] FROM WorkItems
            WHERE [System.TeamProject] = '{project}'
              AND [System.State] <> 'Closed'
              AND [System.State] <> 'Resolved'
              AND [System.State] <> 'Done'
            ORDER BY [System.ChangedDate] DESC
            """
    };
    var result = await client.QueryByWiqlAsync(wiql, project, top: 50, ...);
    // fetch full work items by ID, map to Ticket
}
```

---

## Correct Local Setup Order

Follow this exact order to avoid most of the issues above:

```bash
# 1. Enable K8s in Docker Desktop (Settings → Kubernetes → Enable)
#    Wait until: kubectl get nodes shows docker-desktop Ready

# 2. Fill in .env
cat .env
# ANTHROPIC_API_KEY=sk-ant-...
# AZURE_DEVOPS_TOKEN=...
# SLACK_BOT_TOKEN=xoxb-...
# SLACK_SIGNING_SECRET=...

# 3. Start stack
docker compose up -d redis dispatcher

# 4. Verify health
curl http://localhost:6000/health

# 5. Check K8s is connected
docker compose logs dispatcher | grep -E "INFO|WARN"
# Should see: [INFO] Kubernetes available — 'fix' commands enabled.

# 6. Start ngrok
ngrok http 6000

# 7. Update Slack Event Subscriptions + Interactivity URLs with ngrok URL

# 8. Invite bot to channel: /invite @Agent Smith

# 9. Test
#    list tickets in agent-smith-test
#    fix #55 in agent-smith-test
```

---

## Quick Diagnostics

```bash
# Is the dispatcher running and healthy?
curl http://localhost:6000/health

# Is K8s connected?
docker compose logs dispatcher | grep -E "Kubernetes|WARN|INFO"

# Are events reaching the dispatcher?
docker compose logs dispatcher -f
# (then send a message in Slack and watch for POST /slack/events)

# Is K8s working?
kubectl get nodes
kubectl get pods -n agentsmith   # after first fix command

# Redis working?
docker compose exec redis redis-cli ping
# Expected: PONG
```
