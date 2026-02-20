# Phase 19 – Step 1: Dispatcher Dockerfile

## Goal

Create a production-ready multi-stage Dockerfile for the `AgentSmith.Dispatcher`
ASP.NET Core web application. Separate from the existing `Dockerfile` which builds
the CLI agent container.

---

## File: `Dockerfile.dispatcher`

Located at the repository root, alongside `Dockerfile`.

### Multi-Stage Build

**Stage 1: Build (`mcr.microsoft.com/dotnet/sdk:8.0`)**

- Copy only `.csproj` files first (layer caching for `dotnet restore`)
- Projects to include:
  - `AgentSmith.Domain`
  - `AgentSmith.Contracts`
  - `AgentSmith.Application`
  - `AgentSmith.Infrastructure`
  - `AgentSmith.Dispatcher`
- `dotnet restore src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj`
- Copy all source
- `dotnet publish src/AgentSmith.Dispatcher -c Release -o /app/publish --no-restore`

**Stage 2: Runtime (`mcr.microsoft.com/dotnet/aspnet:8.0`)**

- `aspnet` image (not `runtime`) — required for ASP.NET Core
- Install `curl` for the `HEALTHCHECK` command
- Create non-root user `dispatcher` (uid/gid 1000)
- Copy published output from build stage
- Copy `config/` directory (default config, overridden in K8s via ConfigMap)
- Create `/tmp/agentsmith` with correct ownership
- `USER dispatcher`
- `EXPOSE 8080`
- Set `ASPNETCORE_URLS=http://+:8080` and `ASPNETCORE_ENVIRONMENT=Production`
- `HEALTHCHECK` using `curl -f http://localhost:8080/health`
- `ENTRYPOINT ["dotnet", "AgentSmith.Dispatcher.dll"]`

### Full Dockerfile

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (layer caching)
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj                   src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj             src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj         src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj   src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj           src/AgentSmith.Dispatcher/

RUN dotnet restore src/AgentSmith.Dispatcher/AgentSmith.Dispatcher.csproj

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/AgentSmith.Dispatcher -c Release -o /app/publish --no-restore

# ---

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health check
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user
RUN groupadd --gid 1000 dispatcher && \
    useradd --uid 1000 --gid 1000 --no-create-home dispatcher

COPY --from=build /app/publish .
COPY config/ ./config/

RUN mkdir -p /tmp/agentsmith && chown dispatcher:dispatcher /tmp/agentsmith

USER dispatcher

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AgentSmith.Dispatcher.dll"]
```

---

## Key Differences vs `Dockerfile` (CLI agent)

| Aspect | `Dockerfile` (agent) | `Dockerfile.dispatcher` |
|--------|---------------------|------------------------|
| Base runtime | `dotnet/runtime:8.0` | `dotnet/aspnet:8.0` |
| Entry project | `AgentSmith.Host` | `AgentSmith.Dispatcher` |
| User | `agentsmith` (uid 1000) | `dispatcher` (uid 1000) |
| SSH mount | Yes (`~/.ssh`) | No |
| Port | 8080 (webhook mode) | 8080 (always) |
| ENTRYPOINT | `AgentSmith.Host.dll` | `AgentSmith.Dispatcher.dll` |
| curl install | No | Yes (for HEALTHCHECK) |

---

## Build Commands

```bash
# Build agent image
docker build -t agentsmith:latest .

# Build dispatcher image
docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .
```

---

## Local Test

```bash
docker run --rm \
  -e REDIS_URL=redis://localhost:6379 \
  -e SLACK_BOT_TOKEN=xoxb-test \
  -p 8080:8080 \
  agentsmith-dispatcher:latest

curl http://localhost:8080/health
# → {"status":"ok","timestamp":"..."}
```

---

## Definition of Done

- [ ] `Dockerfile.dispatcher` at repository root
- [ ] Multi-stage build: sdk:8.0 → aspnet:8.0
- [ ] Non-root user `dispatcher` (uid 1000)
- [ ] `curl` installed for HEALTHCHECK
- [ ] `config/` directory copied into image
- [ ] `EXPOSE 8080`
- [ ] `HEALTHCHECK` using `/health` endpoint
- [ ] `docker build -f Dockerfile.dispatcher -t agentsmith-dispatcher:latest .` succeeds
- [ ] `docker run` → `curl /health` returns 200