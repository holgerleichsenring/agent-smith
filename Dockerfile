# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore caching
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Host/AgentSmith.Host.csproj src/AgentSmith.Host/

RUN dotnet restore src/AgentSmith.Host/AgentSmith.Host.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/AgentSmith.Host -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Install git for LibGit2Sharp operations
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd --gid 1000 agentsmith && \
    useradd --uid 1000 --gid agentsmith --create-home agentsmith && \
    mkdir -p /home/agentsmith/.ssh && \
    chown -R agentsmith:agentsmith /home/agentsmith

COPY --from=build /app/publish .
COPY config/ ./config/

# Temp directory for cloned repos
RUN mkdir -p /tmp/agentsmith && chown agentsmith:agentsmith /tmp/agentsmith

USER agentsmith

# Expose webhook listener port (--server mode)
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet AgentSmith.Host.dll --help || exit 1

ENTRYPOINT ["dotnet", "AgentSmith.Host.dll"]
