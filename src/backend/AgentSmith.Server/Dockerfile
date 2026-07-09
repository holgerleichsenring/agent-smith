# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore (layer caching)
COPY src/backend/AgentSmith.Domain/AgentSmith.Domain.csproj                   src/backend/AgentSmith.Domain/
COPY src/backend/AgentSmith.Contracts/AgentSmith.Contracts.csproj             src/backend/AgentSmith.Contracts/
COPY src/backend/AgentSmith.Application/AgentSmith.Application.csproj         src/backend/AgentSmith.Application/
COPY src/backend/AgentSmith.Infrastructure.Core/AgentSmith.Infrastructure.Core.csproj src/backend/AgentSmith.Infrastructure.Core/
COPY src/backend/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj   src/backend/AgentSmith.Infrastructure/
COPY src/backend/AgentSmith.Infrastructure.Persistence/AgentSmith.Infrastructure.Persistence.csproj src/backend/AgentSmith.Infrastructure.Persistence/
COPY src/backend/AgentSmith.Server/AgentSmith.Server.csproj                   src/backend/AgentSmith.Server/
COPY src/backend/AgentSmith.Sandbox.Wire/AgentSmith.Sandbox.Wire.csproj       src/backend/AgentSmith.Sandbox.Wire/

RUN dotnet restore src/backend/AgentSmith.Server/AgentSmith.Server.csproj

# Schemas embedded into AgentSmith.Application via Link (p0128a, p0315b).
# csproj references ../../.agentsmith/schemas/*.json and
# ../../.agentsmith/phase-spec.schema.json relative to the project dir.
COPY .agentsmith/schemas/ .agentsmith/schemas/
COPY .agentsmith/phase-spec.schema.json .agentsmith/

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/backend/AgentSmith.Server -c Release -o /app/publish --no-restore

# ---

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for the health check. apt-get upgrade pulls current OS security
# patches at build time (defense-in-depth). Note: this image ships no libssh2
# (only curl; Ubuntu's libcurl is built without it), so it carries no exposure
# to CVE-2026-55200 (libssh2 RCE) regardless.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && apt-get upgrade -y \
    && rm -rf /var/lib/apt/lists/*

# Non-root user at UID/GID 1000 (matches k8s runAsUser: 1000) with a real home
# dir — DefaultPaths.ComputeCacheRoot falls back to $HOME/.cache/agentsmith when
# XDG_CACHE_HOME is unset, and AnalyzeProjectHandler.PersistCacheAsync writes the
# project-map cache there ($HOME is resolved from /etc/passwd by the runtime).
# .NET 10 base images pre-seed a user at UID 1000 (e.g. `ubuntu`, which carries
# sudo/adm group membership); remove it first so we run as a clean, dedicated
# user with no extra group memberships.
RUN if getent passwd 1000 >/dev/null; then userdel -r "$(getent passwd 1000 | cut -d: -f1)" 2>/dev/null || true; fi && \
    if getent group 1000 >/dev/null; then groupdel "$(getent group 1000 | cut -d: -f1)" 2>/dev/null || true; fi && \
    groupadd --gid 1000 dispatcher && \
    useradd --uid 1000 --gid 1000 --create-home dispatcher

COPY --from=build /app/publish .
# Release version (release-please's source of truth). Read by
# DispatcherBanner at startup so the running binary's version is
# visible in the log — operators no longer have to guess whether a
# pulled image actually carries the fix they expected.
COPY version.txt /app/version.txt

# Config is NOT baked into the image (p0107a). Mount agentsmith.yml at
# /app/config/agentsmith.yml via a K8s ConfigMap volume or a Docker bind-mount.
# If the file is missing, Server fails fast at startup with a clear error.
RUN mkdir -p /app/config /tmp/agentsmith && \
    chown -R dispatcher:dispatcher /app/config /tmp/agentsmith /home/dispatcher

USER dispatcher

EXPOSE 8081

ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8081/health || exit 1

ENTRYPOINT ["dotnet", "AgentSmith.Server.dll"]
