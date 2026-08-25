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
COPY src/backend/AgentSmith.Infrastructure.Persistence.SqlServer/AgentSmith.Infrastructure.Persistence.SqlServer.csproj src/backend/AgentSmith.Infrastructure.Persistence.SqlServer/
COPY src/backend/AgentSmith.Server/AgentSmith.Server.csproj                   src/backend/AgentSmith.Server/
COPY src/backend/AgentSmith.Sandbox.Wire/AgentSmith.Sandbox.Wire.csproj       src/backend/AgentSmith.Sandbox.Wire/
# p0325: build-only dependency of Infrastructure.Core — validates the embedded
# skills tarball (master description lengths) during publish.
COPY src/backend/AgentSmith.SkillsPackaging/AgentSmith.SkillsPackaging.csproj    src/backend/AgentSmith.SkillsPackaging/

RUN dotnet restore src/backend/AgentSmith.Server/AgentSmith.Server.csproj

# Schemas embedded into AgentSmith.Application via Link (p0128a, p0315b).
# 2026-08-25-2103: the root schemas are copied as a SET, not as named files. c9c7 embedded
# context.schema.json and this list did not hear about it, so every image failed at publish
# with CS1566 while the checkout built green. ImageBuildCopiesEmbedsTests holds the pair.
COPY .agentsmith/schemas/ .agentsmith/schemas/
COPY .agentsmith/*.schema.json .agentsmith/

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

# 2026-08-25-8c97: which build this image was made from. The release version above moves
# only on a release commit while this image publishes on every push to the trunk, so the
# revision is the only value that actually tells two builds apart. Unset when the image is
# built by hand, and an image that does not know its build claims no identity at all.
ARG AGENTSMITH_BUILD_REVISION
ARG AGENTSMITH_RELEASE_VERSION
ENV AGENTSMITH_BUILD_REVISION=$AGENTSMITH_BUILD_REVISION
ENV AGENTSMITH_RELEASE_VERSION=$AGENTSMITH_RELEASE_VERSION

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
