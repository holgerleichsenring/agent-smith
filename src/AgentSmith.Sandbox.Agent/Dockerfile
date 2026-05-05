# syntax=docker/dockerfile:1
# Carrier image for AgentSmith.Sandbox.Agent.
# Sole responsibility: deliver the self-contained agent binary at /agent so an
# init-container in the sandbox pod can copy it into a shared volume which the
# main toolchain container then executes. This image SHIPS NO TOOLCHAIN.
# See docs/concepts/sandbox-agent.md for the init-container injection pattern.

# Stage 1: build the self-contained binary
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR src

COPY src/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj src/AgentSmith.Sandbox.Agent/
COPY src/AgentSmith.Sandbox.Wire/AgentSmith.Sandbox.Wire.csproj src/AgentSmith.Sandbox.Wire/
RUN dotnet restore src/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj \
    -r linux-${TARGETARCH:-x64}

COPY src/AgentSmith.Sandbox.Agent/ src/AgentSmith.Sandbox.Agent/
COPY src/AgentSmith.Sandbox.Wire/ src/AgentSmith.Sandbox.Wire/
RUN dotnet publish src/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj \
    -c Release \
    -r linux-${TARGETARCH:-x64} \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:PublishTrimmed=false \
    -o /publish
# Note: IncludeAllContentForSelfExtract (not IncludeNativeLibrariesForSelfExtract)
# is required. With NativeLibraries-only, the managed assemblies stay in-bundle
# but CoreCLR fails to register them in TPA at startup (empty TRUSTED_PLATFORM_
# ASSEMBLIES list, then "/System.Private.CoreLib.dll cannot be found"). Switching
# to AllContent extracts the entire bundle to $DOTNET_BUNDLE_EXTRACT_BASE_DIR
# (defaults to /tmp/.net/...) on first launch and CoreCLR loads cleanly.
# Cost: a one-time disk write (~75 MB) per fresh container. Acceptable for our
# pod lifetime model — the extraction stays around for the full pipeline.

# Stage 2: minimal carrier — Microsoft's official runtime-deps image, which is
# the documented minimal base for self-contained .NET 8 binaries (provides
# glibc + libgcc + libstdc++ + libz + libssl + ICU stubs). Distroless variants
# were tried and rejected: distroless/cc lacks libz which self-contained .NET
# needs at startup; distroless/base lacks libgcc and libstdc++. ENTRYPOINT is
# the agent itself in --inject mode; default CMD writes to /shared/agent (the
# conventional emptyDir mount point the Server pod creates in p0116). USER is
# deliberately unset — the Server pod chooses the runtime UID (typically
# matched to the toolchain main container's UID via fsGroup).
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim
COPY --from=build --chmod=755 /publish/AgentSmith.Sandbox.Agent /agent
ENTRYPOINT ["/agent"]
CMD ["--inject", "/shared/agent"]
