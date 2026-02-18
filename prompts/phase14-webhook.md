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
