# syntax=docker/dockerfile:1
# Carrier image for AgentSmith.Sandbox.Agent.
# Sole responsibility: deliver the self-contained agent binary at /agent so an
# init-container in the sandbox pod can copy it into a shared volume which the
# main toolchain container then executes. This image SHIPS NO TOOLCHAIN.
# See docs/concepts/sandbox-agent.md for the init-container injection pattern.

# Stage 1: build the self-contained binary
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR src

COPY src/backend/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj src/backend/AgentSmith.Sandbox.Agent/
COPY src/backend/AgentSmith.Sandbox.Wire/AgentSmith.Sandbox.Wire.csproj src/backend/AgentSmith.Sandbox.Wire/
RUN dotnet restore src/backend/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj \
    -r linux-${TARGETARCH:-x64}

COPY src/backend/AgentSmith.Sandbox.Agent/ src/backend/AgentSmith.Sandbox.Agent/
COPY src/backend/AgentSmith.Sandbox.Wire/ src/backend/AgentSmith.Sandbox.Wire/
RUN dotnet publish src/backend/AgentSmith.Sandbox.Agent/AgentSmith.Sandbox.Agent.csproj \
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

# Stage 1b (p0357): fetch the relocatable CPython payload. python-build-standalone
# (astral) install_only builds need exactly the glibc floor the self-contained .NET
# agent already requires — python3 therefore works wherever the agent itself runs,
# i.e. in EVERY sandbox toolchain image, without touching those images. Pinned
# version + per-arch sha256; verified before extraction. stdlib only, no network
# pip at runtime. The injector copies /python next to the agent binary at init.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS python-fetch
ARG TARGETARCH
RUN set -eux; \
    version="cpython-3.12.13%2B20260718"; \
    case "${TARGETARCH:-amd64}" in \
      amd64) arch="x86_64";  sha256="7eea0959fa425c8aff3ea0a1352ee7d01d794b51439ed8f5fcfa017dbc0ec661";; \
      arm64) arch="aarch64"; sha256="53fb0a17442cd03bcfbc21df8d82e739b4901b87844934e36e54ab5122a55dfa";; \
      *) echo "unsupported TARGETARCH '${TARGETARCH}' for the python payload" >&2; exit 1;; \
    esac; \
    url="https://github.com/astral-sh/python-build-standalone/releases/download/20260718/${version}-${arch}-unknown-linux-gnu-install_only.tar.gz"; \
    curl -fsSL "${url}" -o /tmp/python.tar.gz; \
    echo "${sha256}  /tmp/python.tar.gz" | sha256sum -c -; \
    mkdir -p /python; \
    tar -xzf /tmp/python.tar.gz --strip-components=1 -C /python; \
    rm /tmp/python.tar.gz; \
    /python/bin/python3 --version

# Stage 2: minimal carrier — Microsoft's official runtime-deps image, which is
# the documented minimal base for self-contained .NET binaries (provides
# glibc + libgcc + libstdc++ + libz + libssl + ICU stubs). Distroless variants
# were tried and rejected: distroless/cc lacks libz which self-contained .NET
# needs at startup; distroless/base lacks libgcc and libstdc++. ENTRYPOINT is
# the agent itself in --inject mode; default CMD writes to /shared/agent (the
# conventional emptyDir mount point the Server pod creates in p0116). USER is
# deliberately unset — the Server pod chooses the runtime UID (typically
# matched to the toolchain main container's UID via fsGroup).
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
COPY --from=build --chmod=755 /publish/AgentSmith.Sandbox.Agent /agent
# p0357: the relocatable CPython payload — --inject copies it to /shared/python and
# the agent's ProcessRunner prepends its bin/ to every step's PATH.
COPY --from=python-fetch /python /python
ENTRYPOINT ["/agent"]
CMD ["--inject", "/shared/agent"]
