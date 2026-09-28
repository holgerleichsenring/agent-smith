# Agent Smith

Self-hosted AI orchestration framework. Code, legal, security, workflows.
Your AI. Your infrastructure. Your rules.

## Images

| Image | Purpose |
|-------|---------|
| `holgerleichsenring/agent-smith-cli` | CLI — one-shot commands (`code`, `security-scan`, `api-scan`, `mad`, `legal`, …) and `database migrate` for the server's init container |
| `holgerleichsenring/agent-smith-server` | The server — webhooks, polling, Slack/Teams chat and the run queue on `:8081`; every run executes here and creates its sandboxes through Docker or Kubernetes |
| `holgerleichsenring/agent-smith-sandbox-agent` | The agent inside each sandbox — started by the server per run, never run by hand |

The images are published for `linux/amd64` and `linux/arm64`.

## Deployment Modes

### 1. Standalone `docker run` (one-shot)

```bash
# Fix a ticket
docker run --rm \
  -e ANTHROPIC_API_KEY=sk-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  -v ./config:/app/config \
  holgerleichsenring/agent-smith-cli \
  code --ticket 42 --project my-api

# Security scan a branch
docker run --rm \
  -e ANTHROPIC_API_KEY=sk-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ./config:/app/config \
  holgerleichsenring/agent-smith-cli \
  security-scan --agent claude-default --branch feature/x --output console
```

Run `docker run --rm holgerleichsenring/agent-smith-cli --help` for the full subcommand list.

### 2. Docker Compose

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
cp .env.example .env  # add your API keys

# CLI one-shot
docker compose -f deploy/docker-compose.yml run --rm agentsmith \
  code --ticket 42 --project my-api

# Long-running stack (Server + Redis)
docker compose -f deploy/docker-compose.yml up -d server redis
```

The Server container handles Slack/Teams chat, webhooks, polling, and queue consumption in one process, and runs every pipeline itself. See [`deploy/docker-compose.yml`](https://github.com/holgerleichsenring/agent-smith/blob/main/deploy/docker-compose.yml) for the full service set (CLI one-shot, Server, Redis, optional Ollama).

### 3. Kubernetes

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith/deploy/k8s
cp 4-secret-template.yaml 4-secret.yaml  # fill in tokens
kubectl apply -f .
```

The `agent-smith-server` deployment takes webhooks, polls trackers and listens for Slack/Teams events; every run executes in the server and creates its sandbox pods in the same namespace. Manifests in [`deploy/k8s/`](https://github.com/holgerleichsenring/agent-smith/tree/main/deploy/k8s).

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ANTHROPIC_API_KEY` | Yes\* | Anthropic API key |
| `OPENAI_API_KEY` | Yes\* | OpenAI API key |
| `GEMINI_API_KEY` | Yes\* | Google Gemini API key |
| `GITHUB_TOKEN` | Yes\*\* | GitHub PAT |
| `AZURE_DEVOPS_TOKEN` | Yes\*\* | Azure DevOps PAT |
| `REDIS_URL` | Server only | Redis connection (queue, leases, sandbox channels) |
| `SLACK_BOT_TOKEN` | Server only | Slack adapter |
| `SLACK_SIGNING_SECRET` | Server only | Slack request verification |

\* At least one AI provider key required. \*\* At least one source/ticket platform token required.

## Supported Platforms

- **Tickets**: GitHub Issues, GitLab Issues, Jira, Azure DevOps Work Items
- **Source**: GitHub, GitLab, Azure Repos, Local
- **AI**: Anthropic Claude, OpenAI, Google Gemini, Ollama (local)

## Links

- [GitHub Repository](https://github.com/holgerleichsenring/agent-smith)
- [Configuration Guide](https://github.com/holgerleichsenring/agent-smith/tree/main/config)
- [Deployment Manifests](https://github.com/holgerleichsenring/agent-smith/tree/main/deploy)
