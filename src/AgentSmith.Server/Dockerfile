# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (layer caching)
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj                   src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj             src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj         src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj src/AgentSmith.Infrastructure.Core/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj   src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Server/AgentSmith.Server.csproj           src/AgentSmith.Server/

RUN dotnet restore src/AgentSmith.Server/AgentSmith.Server.csproj

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/AgentSmith.Server -c Release -o /app/publish --no-restore

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

EXPOSE 8081

ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8081/health || exit 1

ENTRYPOINT ["dotnet", "AgentSmith.Server.dll"]
