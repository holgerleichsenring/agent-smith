# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore caching
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj src/AgentSmith.Infrastructure.Core/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Cli/AgentSmith.Cli.csproj src/AgentSmith.Cli/

RUN dotnet restore src/AgentSmith.Cli/AgentSmith.Cli.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/AgentSmith.Cli -c Release -o /app/publish --no-restore

# Stage 2: Runtime
# TODO p4x: dotnet SDK required until TestCommand spawns ephemeral job containers.
# Target: aspnet:8.0 base + job-based test execution (see DockerJobSpawner).
# Agent Smith is language-agnostic — hardcoding SDK is technical debt.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime
WORKDIR /app

# Install git for LibGit2Sharp operations, gosu for permission dropping
RUN apt-get update && apt-get install -y --no-install-recommends git libgit2-dev gosu && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd --gid 1000 agentsmith && \
    useradd --uid 1000 --gid agentsmith --create-home agentsmith && \
    mkdir -p /home/agentsmith/.ssh && \
    chown -R agentsmith:agentsmith /home/agentsmith

COPY --from=build /app/publish .
COPY config/ ./config/

# Output directory for reports (markdown, sarif)
RUN mkdir -p /output && chown agentsmith:agentsmith /output
RUN mkdir -p /tmp/agentsmith && chown agentsmith:agentsmith /tmp/agentsmith

# Entrypoint fixes volume mount permissions, then drops to agentsmith user
COPY src/AgentSmith.Cli/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# Expose webhook listener port (--server mode)
EXPOSE 8081

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet AgentSmith.Cli.dll --help || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
