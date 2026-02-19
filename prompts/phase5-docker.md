# Phase 5 - Step 2: Docker

## Goal
Multi-stage Dockerfile for a lean runtime image.
Project root: `Dockerfile`, `.dockerignore`

---

## Dockerfile

```
File: Dockerfile (project root)
```

**Stage 1: Build**
- Base: `mcr.microsoft.com/dotnet/sdk:8.0`
- Copy solution + all csproj (for restore caching)
- `dotnet restore`
- Copy rest + `dotnet publish -c Release`

**Stage 2: Runtime**
- Base: `mcr.microsoft.com/dotnet/runtime:8.0`
- Copy published output
- Copy `config/` as default config
- ENTRYPOINT: `dotnet AgentSmith.Host.dll`

---

## .dockerignore

```
File: .dockerignore (project root)
```

Exclude:
- `bin/`, `obj/`, `.git/`, `node_modules/`
- `*.md` (except config/coding-principles.md)
- `.vs/`, `.idea/`, `*.user`
- `tests/` (not in the runtime image)

---

## Docker Compose (Example)

```
File: docker-compose.yml (project root)
```

For local development / demo:
```yaml
services:
  agentsmith:
    build: .
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - AZURE_DEVOPS_TOKEN=${AZURE_DEVOPS_TOKEN}
    volumes:
      - ./config:/app/config
      - ~/.ssh:/root/.ssh:ro
```

---

## Image Size

Target: < 200MB
- Runtime base: ~85MB
- Published app: ~30-50MB (self-contained would be ~80MB, but framework-dependent is sufficient)
- Total: ~120-150MB

---

## Testing

```bash
# Build
docker build -t agentsmith .

# Show help
docker run --rm agentsmith --help

# Dry run
docker run --rm \
  -v $(pwd)/config:/app/config \
  agentsmith --dry-run "fix #123 in todo-list"

# Real run
docker run --rm \
  -e GITHUB_TOKEN=ghp_xxx \
  -e ANTHROPIC_API_KEY=sk-xxx \
  -v $(pwd)/config:/app/config \
  agentsmith "fix #123 in todo-list"
```
