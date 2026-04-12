# Phase 52: Single Executable Release

## Goal

Publish agent-smith as a self-contained single-file executable for
linux-x64, linux-arm64, osx-x64, osx-arm64, and win-x64.
GitHub Releases with downloadable binaries on git tags.

## Motivation

Docker-in-Docker for CI/CD pipelines is fragile (socket permissions,
volume mounts, Docker-from-Docker). A single binary eliminates all of that:

```bash
curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64 \
  -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith

agent-smith api-scan --swagger https://api.example.com/swagger.json \
  --target https://api.example.com --output console,markdown
```

No Docker pull, no socket, no volumes, no permissions. Tool containers
(Nuclei, Spectral) run via DockerToolRunner if Docker is available,
or via ProcessToolRunner if the tools are installed locally.

## Publish Strategy

**PublishSingleFile + Self-Contained, NO Trimming.**

Trimming breaks reflection-heavy dependencies (YamlDotNet, Docker.DotNet,
System.Text.Json, LibGit2Sharp, DI container). Not worth the instability.

Expected binary size: ~70-80 MB per platform. Acceptable for a CLI tool.

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>false</PublishTrimmed>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

`IncludeNativeLibrariesForSelfExtract` is needed for LibGit2Sharp's native
binaries (libgit2) to be embedded in the single file.

## Platforms

| RID | Binary name | Notes |
|-----|------------|-------|
| linux-x64 | agent-smith-linux-x64 | CI/CD agents, servers |
| linux-arm64 | agent-smith-linux-arm64 | ARM servers, Raspberry Pi |
| osx-x64 | agent-smith-osx-x64 | Intel Macs |
| osx-arm64 | agent-smith-osx-arm64 | Apple Silicon |
| win-x64 | agent-smith-win-x64.exe | Windows |

## GitHub Actions Workflow

New workflow `release.yml` triggered on version tags (`v*`):

```yaml
on:
  push:
    tags: ['v*']

jobs:
  build:
    strategy:
      matrix:
        include:
          - rid: linux-x64
            os: ubuntu-latest
          - rid: linux-arm64
            os: ubuntu-latest
          - rid: osx-x64
            os: macos-latest
          - rid: osx-arm64
            os: macos-latest
          - rid: win-x64
            os: windows-latest

    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet publish src/AgentSmith.Cli -c Release
              -r ${{ matrix.rid }} --self-contained
              -p:PublishSingleFile=true
              -p:IncludeNativeLibrariesForSelfExtract=true
              -o ./publish
      - run: mv ./publish/AgentSmith.Cli ./publish/agent-smith-${{ matrix.rid }}
      - uses: actions/upload-artifact@v4
        with:
          name: agent-smith-${{ matrix.rid }}
          path: ./publish/agent-smith-${{ matrix.rid }}*

  release:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
      - uses: softprops/action-gh-release@v2
        with:
          files: agent-smith-*/agent-smith-*
```

## Config File Discovery

The binary needs to find `config/agentsmith.yml` and skill files.
Search order:

1. `--config` CLI flag (explicit path)
2. `./.agentsmith/agentsmith.yml` (project root convention)
3. `./config/agentsmith.yml` (current directory)
4. `~/.agentsmith/agentsmith.yml` (user home)

This already works — the existing `--config` option handles it.
For project-level use (like the AccessPortal), the `.agentsmith/` directory
at the project root contains everything.

## Pipeline Integration (simplified)

Azure DevOps example — no Docker at all:

```yaml
- task: Bash@3
  displayName: "Run agent-smith API security scan"
  continueOnError: true
  env:
    ANTHROPIC_API_KEY: $(ANTHROPIC_API_KEY)
  inputs:
    targetType: "inline"
    script: |
      curl -sL https://github.com/holgerleichsenring/agent-smith/releases/latest/download/agent-smith-linux-x64 \
        -o /usr/local/bin/agent-smith && chmod +x /usr/local/bin/agent-smith
      agent-smith api-scan \
        --swagger https://your-api/swagger.json \
        --target https://your-api \
        --config $(Build.SourcesDirectory)/.agentsmith/agentsmith.yml \
        --output console,markdown \
        --output-dir $(Build.ArtifactStagingDirectory)/security
```

## Docker Entrypoint (permission fix)

The Docker image uses an entrypoint script that starts as root, fixes
volume mount permissions, then drops to `agentsmith` user. This is a
standard pattern (used by Postgres, Redis, Nginx official images).

```dockerfile
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh
ENTRYPOINT ["/app/docker-entrypoint.sh"]
```

```bash
#!/bin/bash
set -e
# Fix permissions on mounted volumes (host mounts are owned by root)
for dir in /output /tmp/agentsmith; do
    [ -d "$dir" ] && chown agentsmith:agentsmith "$dir" 2>/dev/null || true
done
exec gosu agentsmith dotnet AgentSmith.Cli.dll "$@"
```

This eliminates the need for `run.sh` wrapper scripts in consumer projects.
The `run.sh` pattern remains as documentation/convenience but is no longer
required for correct operation.

## Files to Create

- `.github/workflows/release.yml` — multi-platform build + GitHub Release
- `docker-entrypoint.sh` — permission fix + user switch
- Update `README.md` — installation instructions

## Files to Modify

- `src/AgentSmith.Cli/AgentSmith.Cli.csproj` — add PublishSingleFile properties
- `Dockerfile` — install gosu, use entrypoint script

## Definition of Done

- [ ] `dotnet publish` produces working single-file binary for all 5 platforms
- [ ] GitHub Actions workflow builds on tag push
- [ ] GitHub Release contains all 5 binaries
- [ ] Binary runs without .NET runtime installed
- [ ] Binary finds config files in standard locations
- [ ] LibGit2Sharp native libraries embedded correctly
- [ ] Docker entrypoint fixes mount permissions automatically
- [ ] No `run.sh` required for consumers
- [ ] README documents installation via curl
- [ ] README documents simplified pipeline integration
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- None (standalone)
