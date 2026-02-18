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
