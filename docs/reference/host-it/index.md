# Deployment

Agent Smith supports four deployment models, from a single binary on your laptop to a full Kubernetes cluster with chat integration.

## Deployment Options

| Option | Best for | Requirements |
|--------|----------|-------------|
| [Single Binary](binary.md) | CI/CD pipelines, local dev, quick scans | None (self-contained) |
| [Docker](docker.md) | Persistent server, full stack with tools | Docker Engine |
| [Kubernetes](kubernetes.md) | Production, multi-tenant, auto-scaling | K8s cluster, Redis |
| [Chat Gateway](chat-gateway.md) | Team collaboration via Slack/Teams | K8s or Docker, Redis |

## Quick Decision Guide

```
Do you need chat integration (Slack/Teams)?
├── Yes → Chat Gateway (Dispatcher + K8s/Docker)
└── No
    ├── Running in CI/CD? → Single Binary
    ├── Need tool containers (Nuclei/Spectral)? → Docker
    └── Production with webhooks? → Docker or Kubernetes
```

!!! tip "Start Simple"
    Most users start with the single binary for local testing, then move to Docker Compose for team use. Add Kubernetes and the Dispatcher only when you need chat-driven workflows or multi-tenant isolation.

## Common Configuration

All deployment modes read the same YAML configuration. The discovery order is:

1. `--config` CLI flag (explicit path)
2. `.agentsmith/agentsmith.yml` (project-local)
3. `config/agentsmith.yml` (working directory)
4. `~/.agentsmith/agentsmith.yml` (user home)

See [Configuration](../configuration/index.md) for the full reference.

## API Keys

Every deployment needs at least one AI provider key:

| Variable | Provider | Required |
|----------|----------|----------|
| `ANTHROPIC_API_KEY` | Claude | Recommended (default) |
| `OPENAI_API_KEY` | GPT-4 | Optional |
| `GEMINI_API_KEY` | Gemini | Optional |
| `GITHUB_TOKEN` | GitHub repos/issues | For GitHub projects |
| `AZURE_DEVOPS_TOKEN` | Azure DevOps | For Azure projects |
| `GITLAB_TOKEN` | GitLab | For GitLab projects |
