# Phase 14: GitHub Action & Webhook Trigger - Implementation Plan

## Goal
Agent Smith reacts automatically to issues. No manual start required.
Two variants: GitHub Action (zero infrastructure) and webhook listener (self-hosted).

---

## Prerequisite
- Phase 10 completed (headless mode)
- Phase 13 completed (ticket writeback for feedback)

## Steps

### Step 1: GitHub Action
See: `prompts/phase14-github-action.md`

Workflow file triggered by `issues.labeled` event with `agent-smith` label.
Project: `.github/workflows/`

### Step 2: Webhook Listener
See: `prompts/phase14-webhook.md`

`--server` mode with minimal HTTP endpoint for webhook events.
Project: `AgentSmith.Host/`

### Step 3: Docker Compose Server Service
Update docker-compose.yml with `agentsmith-server` service.
Project: `docker-compose.yml`, `Dockerfile`

### Step 4: Verify

---

## Dependencies

```
Step 1 (GitHub Action) ──────── independent
Step 2 (Webhook Listener) ───┐
Step 3 (Docker Compose) ─────┘── Step 4 (Verify)
```

---

## NuGet Packages (Phase 14)

No new packages required. Webhook uses built-in `System.Net.HttpListener`.

---

## Key Decisions

1. **Both variants** - GitHub Action for quick start (zero infrastructure), webhook listener for self-hosted/K8s
2. GitHub Action uses `AGENT_SMITH_TOKEN` secret (PAT with repo+issues permissions)
3. Webhook listener uses `HttpListener` (no ASP.NET dependency needed)
4. Trigger label configurable but defaults to `agent-smith`
5. Server mode: `--server --port 8080`, graceful shutdown via Ctrl+C
6. Each webhook request spawns an async task (non-blocking)
7. Health check endpoint at `GET /health`

---

## Definition of Done (Phase 14)
- [ ] `.github/workflows/agent-smith.yml` triggered by `issues.labeled`
- [ ] `WebhookListener` class with POST /webhook and GET /health
- [ ] `--server` and `--port` CLI options in Program.cs
- [ ] `RunServerMode()` with CancellationToken and graceful shutdown
- [ ] Input argument now optional (required only in non-server mode)
- [ ] docker-compose.yml: separate `agentsmith` (one-shot) and `agentsmith-server` services
- [ ] Dockerfile: EXPOSE 8080
- [ ] All existing tests green


---

# Phase 14: GitHub Action - Implementation Details

## Overview
GitHub Actions workflow that triggers when the `agent-smith` label is added to an issue.
Builds Agent Smith from source, runs it in headless mode against the labeled issue.

---

## Workflow File

`.github/workflows/agent-smith.yml`

### Trigger
```yaml
on:
  issues:
    types: [labeled]

jobs:
  agent-smith:
    if: github.event.label.name == 'agent-smith'
```

Only runs when the specific label is added. Other labels are ignored.

### Steps
1. **Checkout** - `actions/checkout@v4`
2. **Setup .NET** - `actions/setup-dotnet@v4` with 8.0.x
3. **Build** - `dotnet build --configuration Release`
4. **Run** - `dotnet run` with `--headless` and issue number from event context

### Environment Variables
```yaml
env:
  GITHUB_TOKEN: ${{ secrets.AGENT_SMITH_TOKEN || secrets.GITHUB_TOKEN }}
  ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
  OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
  GEMINI_API_KEY: ${{ secrets.GEMINI_API_KEY }}
```

### Input Construction
```bash
"fix #${{ github.event.issue.number }} in ${{ github.event.repository.name }}"
```

---

## Required Secrets
- `ANTHROPIC_API_KEY` (or `OPENAI_API_KEY` / `GEMINI_API_KEY` depending on config)
- `AGENT_SMITH_TOKEN` - GitHub PAT with `repo` + `issues` permissions
  - Falls back to `GITHUB_TOKEN` if not set
  - Note: `GITHUB_TOKEN` cannot trigger other workflows on the created PR

---

## Timeout
`timeout-minutes: 30` - prevents runaway costs on infinite loops.


---

# Phase 14: Webhook Listener - Implementation Details

## Overview
Minimal HTTP server for receiving GitHub/Azure DevOps webhook events.
Runs as `--server` mode in the same binary - no separate service needed.

---

## WebhookListener Class (Host Layer)

```csharp
public sealed class WebhookListener(
    IServiceProvider services,
    string configPath,
    ILogger<WebhookListener> logger)
```

Uses `System.Net.HttpListener` - no ASP.NET dependency required.

### Endpoints
- `POST /webhook` - Receives webhook payloads, triggers runs
- `GET /health` - Returns "ok" (for container health checks / load balancers)
- Everything else returns 404

### Request Handling
Each incoming request is processed in a separate `Task.Run()` to avoid blocking
the listener. The webhook returns 202 Accepted immediately, then processes async.

---

## GitHub Event Parsing

```csharp
private string? TryParseGitHubEvent(string body, NameValueCollection headers)
```

1. Check `X-GitHub-Event` header equals `"issues"`
2. Parse JSON body
3. Check `action == "labeled"`
4. Check `label.name == "agent-smith"` (case-insensitive)
5. Extract `issue.number` and `repository.name`
6. Return `"fix #{number} in {repoName}"`

Non-matching events return null and get a 200 "Event ignored" response.

---

## Run Execution

```csharp
private async Task ExecuteRunAsync(string input)
```

Resolves `ProcessTicketUseCase` from DI container, calls `ExecuteAsync` with
`headless: true`. Exceptions are caught and logged but don't crash the server.

---

## Program.cs Integration

### New CLI Options
```csharp
var serverOption = new Option<bool>("--server", "Start as webhook listener");
var portOption = new Option<int>("--port", () => 8080, "Webhook listener port");
```

### Input Argument
Changed from required to optional (default: empty string).
In non-server mode, validates that input is provided.

### RunServerMode
```csharp
static async Task RunServerMode(ServiceProvider provider, string configPath, int port)
{
    var listener = new WebhookListener(provider, configPath, logger);
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    await listener.RunAsync(port, cts.Token);
}
```

Graceful shutdown via Ctrl+C / SIGTERM.

---

## Docker Compose

Two services in docker-compose.yml:

### agentsmith (one-shot)
```yaml
restart: "no"
# Usage: docker compose run --rm agentsmith --headless "fix #1 in my-project"
```

### agentsmith-server (persistent)
```yaml
restart: unless-stopped
ports:
  - "${WEBHOOK_PORT:-8080}:8080"
command: ["--server", "--port", "8080"]
```

---

## Dockerfile
Add `EXPOSE 8080` for the webhook port.
Health check remains the same (`--help` based).
