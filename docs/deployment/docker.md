# Docker

The Docker image includes everything needed to run Agent Smith, including SSH and Git tooling.

**Image:** `holgerleichsenring/agent-smith:latest`

## Quick Start

### Fix a Bug

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -v ~/.ssh:/home/agentsmith/.ssh:ro \
  holgerleichsenring/agent-smith:latest \
  fix --repo https://github.com/org/repo --ticket 42
```

### API Scan with Docker Socket

Tool containers (Nuclei, Spectral) need access to the Docker socket:

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
  -v $(pwd):/app/repo \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v $(pwd)/results:/app/output \
  holgerleichsenring/agent-smith:latest \
  api-scan --repo /app/repo --output console,sarif --output-dir /app/output
```

### Security Scan

```bash
docker run --rm \
  -e ANTHROPIC_API_KEY=$ANTHROPIC_API_KEY \
  -v $(pwd):/app/repo \
  holgerleichsenring/agent-smith:latest \
  security-scan --repo /app/repo --output console,markdown --output-dir /app/repo/results
```

## Docker Compose — Full Stack

The `docker-compose.yml` provides the complete setup:

```yaml
services:
  # One-shot agent (run commands ad-hoc)
  agentsmith:
    image: holgerleichsenring/agent-smith:latest
    restart: "no"
    env_file: .env
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN:-}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY:-}
    volumes:
      - ./config:/app/config
      - ${SSH_KEY_PATH:-~/.ssh}:/home/agentsmith/.ssh:ro
      - /var/run/docker.sock:/var/run/docker.sock

  # Webhook server (persistent, listens for GitHub/GitLab/AzDO events)
  agentsmith-server:
    image: holgerleichsenring/agent-smith:latest
    restart: unless-stopped
    env_file: .env
    ports:
      - "${WEBHOOK_PORT:-8081}:8081"
    volumes:
      - ./config:/app/config
      - ${SSH_KEY_PATH:-~/.ssh}:/home/agentsmith/.ssh:ro
    command: ["server", "--port", "8081"]

  # Redis (required for Dispatcher)
  redis:
    image: redis:7-alpine
    restart: unless-stopped
    command: ["--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru", "--save", ""]
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 3

  # Dispatcher (Slack/Teams gateway, spawns Docker containers per request)
  dispatcher:
    build:
      context: .
      dockerfile: Dockerfile.dispatcher
    restart: unless-stopped
    depends_on:
      redis:
        condition: service_healthy
    env_file: .env
    environment:
      - REDIS_URL=redis:6379
      - SPAWNER_TYPE=docker
      - SLACK_BOT_TOKEN=${SLACK_BOT_TOKEN:-}
      - SLACK_SIGNING_SECRET=${SLACK_SIGNING_SECRET:-}
      - AGENTSMITH_IMAGE=${AGENTSMITH_IMAGE:-holgerleichsenring/agent-smith:latest}
    ports:
      - "${DISPATCHER_PORT:-6000}:8081"
    volumes:
      - ./config:/app/config
      - /var/run/docker.sock:/var/run/docker.sock

  # Ollama (optional, for local models)
  ollama:
    image: ollama/ollama
    profiles: [local-models]
    volumes:
      - ollama-data:/root/.ollama
    ports:
      - "11434:11434"
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]

volumes:
  ollama-data:
```

### Running Commands

```bash
# One-shot: fix a bug
docker compose run --rm agentsmith fix --repo https://github.com/org/repo --ticket 42

# One-shot: security scan
docker compose run --rm agentsmith security-scan --repo /app/repo --output console

# Start webhook server
docker compose up -d agentsmith-server

# Start full stack (dispatcher + redis)
docker compose up -d dispatcher redis

# Start with local Ollama models
docker compose --profile local-models up -d
```

## Entrypoint Permission Handling

The Docker image uses `gosu` to handle volume permission mismatches automatically. When the container starts:

1. The entrypoint detects the UID/GID of mounted volumes
2. It adjusts the `agentsmith` user to match the host's file ownership
3. It drops privileges via `gosu` before running the command

This means you never need to worry about file permission issues with mounted volumes — output files are owned by your host user.

!!! info "No manual UID mapping needed"
    Unlike many Docker images, you do not need to pass `--user $(id -u):$(id -g)`. The entrypoint handles this automatically.

## Environment Variables

Create a `.env` file:

```bash
ANTHROPIC_API_KEY=sk-ant-...
GITHUB_TOKEN=ghp_...
OPENAI_API_KEY=sk-...           # optional
GEMINI_API_KEY=...              # optional
AZURE_DEVOPS_TOKEN=...          # optional
SLACK_BOT_TOKEN=xoxb-...       # for Dispatcher
SLACK_SIGNING_SECRET=...       # for Dispatcher
```

## Configuration

Mount your config file:

```bash
docker run --rm \
  -v ./config:/app/config \
  holgerleichsenring/agent-smith:latest \
  ...
```

The container looks for configuration at:

1. `--config` flag
2. `.agentsmith/agentsmith.yml`
3. `config/agentsmith.yml` (default mount point)
4. `~/.agentsmith/agentsmith.yml`
