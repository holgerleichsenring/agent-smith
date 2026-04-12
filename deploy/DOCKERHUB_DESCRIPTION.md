# Agent Smith

Self-hosted AI orchestration framework. Code, legal, security, workflows.
Your AI. Your infrastructure. Your rules.

## Quick Start

```bash
# One-shot: process a ticket
docker run --rm \
  -e ANTHROPIC_API_KEY=sk-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  -v ./config:/app/config \
  holgerleichsenring/agent-smith \
  "fix #42 in my-api"

# Server mode: webhook listener
docker run -d \
  -e ANTHROPIC_API_KEY=sk-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  -v ./config:/app/config \
  -p 8081:8081 \
  holgerleichsenring/agent-smith \
  --server --port 8081
```

## Docker Compose

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
cp .env.example .env  # add your API keys
docker compose up -d
```

## Images

| Image | Description |
|-------|-------------|
| `holgerleichsenring/agent-smith` | CLI runner + webhook server |
| `holgerleichsenring/agent-smith-dispatcher` | Slack/Teams gateway, K8s/Docker job spawner |

## Supported Platforms

- **Tickets**: GitHub Issues, GitLab Issues, Jira, Azure DevOps Work Items
- **Source**: GitHub, GitLab, Azure Repos, Local
- **AI**: Anthropic Claude, OpenAI, Google Gemini

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ANTHROPIC_API_KEY` | Yes* | Anthropic API key |
| `GITHUB_TOKEN` | Yes* | GitHub personal access token |
| `OPENAI_API_KEY` | No | OpenAI API key (if using OpenAI provider) |
| `GEMINI_API_KEY` | No | Google Gemini API key |
| `AZURE_DEVOPS_TOKEN` | No | Azure DevOps PAT |
| `REDIS_URL` | No | Redis connection (for dispatcher mode) |

*At least one AI provider key and one source provider token required.

## Links

- [GitHub Repository](https://github.com/holgerleichsenring/agent-smith)
- [Configuration Guide](https://github.com/holgerleichsenring/agent-smith/blob/main/config/)
