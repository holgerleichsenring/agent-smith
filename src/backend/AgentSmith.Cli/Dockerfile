# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore caching
COPY src/backend/AgentSmith.Domain/AgentSmith.Domain.csproj src/backend/AgentSmith.Domain/
COPY src/backend/AgentSmith.Contracts/AgentSmith.Contracts.csproj src/backend/AgentSmith.Contracts/
COPY src/backend/AgentSmith.Application/AgentSmith.Application.csproj src/backend/AgentSmith.Application/
COPY src/backend/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj src/backend/AgentSmith.Infrastructure.Core/
COPY src/backend/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj src/backend/AgentSmith.Infrastructure/
COPY src/backend/AgentSmith.Infrastructure.Persistence/AgentSmith.Infrastructure.Persistence.csproj src/backend/AgentSmith.Infrastructure.Persistence/
COPY src/backend/AgentSmith.Cli/AgentSmith.Cli.csproj src/backend/AgentSmith.Cli/
COPY src/backend/AgentSmith.Sandbox.Wire/AgentSmith.Sandbox.Wire.csproj src/backend/AgentSmith.Sandbox.Wire/

RUN dotnet restore src/backend/AgentSmith.Cli/AgentSmith.Cli.csproj

# Schemas embedded into AgentSmith.Application via Link (p0128a, p0315b).
# csproj references ../../.agentsmith/schemas/*.json and
# ../../.agentsmith/phase-spec.schema.json relative to the project dir.
COPY .agentsmith/schemas/ .agentsmith/schemas/
COPY .agentsmith/phase-spec.schema.json .agentsmith/

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/backend/AgentSmith.Cli -c Release -o /app/publish --no-restore

# Stage 2: Runtime
# TODO p4x: dotnet SDK required until TestCommand spawns ephemeral job containers.
# Target: aspnet:8.0 base + job-based test execution (see DockerJobSpawner).
# Agent Smith is language-agnostic — hardcoding SDK is technical debt.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

# Install git for LibGit2Sharp operations, gosu for permission dropping.
# apt-get upgrade pulls current OS security patches at build time (defense-in-
# depth). git/libgit2 bring in libssh2, the component behind CVE-2026-55200 (RCE);
# the .NET 10 base is Ubuntu 24.04, which is flagged "not affected" for that CVE,
# so the exposure is closed by the base itself — the fix was never a .NET TFM bump.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git libgit2-dev gosu \
    && apt-get upgrade -y \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user at UID/GID 1000. .NET 10 base images pre-seed a user at
# UID 1000 (e.g. `ubuntu`); remove it first so `agentsmith` owns 1000 — the
# entrypoint drops to `agentsmith` by name via gosu, and k8s/fsGroup convention
# expects 1000.
RUN if getent passwd 1000 >/dev/null; then userdel -r "$(getent passwd 1000 | cut -d: -f1)" 2>/dev/null || true; fi && \
    if getent group 1000 >/dev/null; then groupdel "$(getent group 1000 | cut -d: -f1)" 2>/dev/null || true; fi && \
    groupadd --gid 1000 agentsmith && \
    useradd --uid 1000 --gid agentsmith --create-home agentsmith && \
    mkdir -p /home/agentsmith/.ssh && \
    chown -R agentsmith:agentsmith /home/agentsmith

COPY --from=build /app/publish .

# Config is NOT baked into the image (p0107a). Either pass --config <path>
# explicitly, or mount your agentsmith.yml at /app/config/agentsmith.yml
# (matches ConfigDiscovery's fallback search path). Without one of those
# the CLI fails fast at command time with a clear error.
RUN mkdir -p /app/config && chown agentsmith:agentsmith /app/config

# Output directory for reports (markdown, sarif)
RUN mkdir -p /output && chown agentsmith:agentsmith /output
RUN mkdir -p /tmp/agentsmith && chown agentsmith:agentsmith /tmp/agentsmith

# Relational-persistence SQLite dir (p0246). Pre-created + owned so the default
# `Data Source=/var/lib/agentsmith/agentsmith.db` works even without a mounted
# volume; the entrypoint re-chowns it when a fresh named volume IS mounted.
RUN mkdir -p /var/lib/agentsmith && chown agentsmith:agentsmith /var/lib/agentsmith

# Entrypoint fixes volume mount permissions, then drops to agentsmith user
COPY src/backend/AgentSmith.Cli/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# Expose webhook listener port (--server mode)
EXPOSE 8081

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet AgentSmith.Cli.dll --help || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
