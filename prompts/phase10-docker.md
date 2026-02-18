# Phase 10: Docker Production Setup - Implementation Details

## Overview
Production-ready container setup: non-root user, health check, environment-based
configuration, SSH key mount for git operations.

---

## Dockerfile Changes

### Non-root user
```dockerfile
RUN groupadd --gid 1000 agentsmith && \
    useradd --uid 1000 --gid agentsmith --create-home agentsmith && \
    mkdir -p /home/agentsmith/.ssh && \
    chown -R agentsmith:agentsmith /home/agentsmith

USER agentsmith
```

### Health check
```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet AgentSmith.Host.dll --help || exit 1
```

### Temp directory for cloned repos
```dockerfile
RUN mkdir -p /tmp/agentsmith && chown agentsmith:agentsmith /tmp/agentsmith
```

---

## docker-compose.yml

```yaml
services:
  agentsmith:
    build: .
    restart: unless-stopped
    stdin_open: false          # No interactive mode in container
    env_file:
      - .env                   # All secrets from .env file
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
    volumes:
      - ./config:/app/config
      - ${SSH_KEY_PATH:-~/.ssh}:/home/agentsmith/.ssh:ro  # Read-only SSH keys
```

Key decisions:
- `stdin_open: false` - container runs headless
- `restart: unless-stopped` - survives Docker daemon restarts
- SSH keys mounted read-only
- Config directory mounted for easy editing

---

## .env.example

Template file with all possible environment variables and comments.
Users copy to `.env` and fill in their values.

---

## config/agentsmith.example.yml

Minimal quick-start configuration with comments explaining each section.
