# Phase 49: Tool Runner Abstraction

## Problem

All tool container operations (Nuclei, Spectral) use `DockerContainerRunner`
which requires a Docker Socket. This creates issues across deployment modes:

- **Docker-from-Docker**: Requires socket mount + shared `/tmp` mount hack
- **K8s**: No Docker socket available — tools can't run at all
- **Podman**: Socket path differs, rootless has different semantics
- **Local dev**: Unnecessary container overhead when tools are installed

## Solution

Replace the single `IContainerRunner` with a `IToolRunner` abstraction
that has multiple implementations selectable via configuration.

### Interface

```csharp
public interface IToolRunner
{
    Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken ct);
}

public sealed record ToolRunRequest(
    string Tool,                              // "nuclei", "spectral"
    IReadOnlyList<string> Arguments,
    Dictionary<string, string>? InputFiles,   // filename → content
    string? OutputFileName,                   // expected output file
    int TimeoutSeconds = 300);

public sealed record ToolResult(
    string Stdout,
    string Stderr,
    string? OutputFileContent,
    int ExitCode,
    int DurationSeconds);
```

### Implementations

| Implementation | Mode | How it works |
|---------------|------|-------------|
| `DockerToolRunner` | Docker socket | Current approach — spawns sibling container via socket |
| `PodmanToolRunner` | Podman socket | Same as Docker but different socket path |
| `K8sJobToolRunner` | K8s cluster | Creates ephemeral K8s Job, reads logs + output via K8s API |
| `ProcessToolRunner` | Local install | Runs `nuclei`/`spectral` as child process directly |

### Configuration

```yaml
tool_runner:
  type: docker  # docker | podman | process | k8s

  # docker/podman only:
  socket: unix:///var/run/docker.sock

  # k8s only:
  namespace: default
  image_pull_policy: IfNotPresent

  # process only (optional, auto-detected from PATH):
  nuclei_path: /usr/local/bin/nuclei
  spectral_path: /usr/local/bin/spectral
```

### Tool Image Registry

Map tool names to container images (used by Docker/Podman/K8s runners):

```yaml
tool_runner:
  images:
    nuclei: projectdiscovery/nuclei:latest
    spectral: stoplight/spectral:6
```

### Migration Path

1. `NucleiSpawner` and `SpectralSpawner` switch from `IContainerRunner` to
   `IToolRunner` — they prepare a `ToolRunRequest` instead of a
   `ContainerRunRequest`
2. The runner handles temp dirs, volume mounts, and cleanup internally
3. `DockerToolRunner` encapsulates the shared-temp-path logic
4. `IContainerRunner` stays for backward compatibility (Dispatcher still
   uses it) but `IToolRunner` is the new preferred interface for tools

### Deployment Matrix

| Scenario | tool_runner.type | Notes |
|----------|-----------------|-------|
| CI/CD Pipeline (api-scan) | docker | Socket + shared /tmp mount via docker-compose |
| docker-compose dev | docker | Socket mount in docker-compose.yml |
| K8s production | k8s | No socket needed, uses K8s Job API |
| Local development | process | Tools installed via brew/npm/go install |
| Podman rootless | podman | Different socket path, no root needed |

## Files to Create

- `src/AgentSmith.Contracts/Providers/IToolRunner.cs`
- `src/AgentSmith.Infrastructure/Services/Tools/DockerToolRunner.cs`
- `src/AgentSmith.Infrastructure/Services/Tools/ProcessToolRunner.cs`
- `src/AgentSmith.Infrastructure/Services/Tools/K8sJobToolRunner.cs`
- `src/AgentSmith.Infrastructure/Services/Tools/PodmanToolRunner.cs`
- Config model for `tool_runner` section

## Files to Modify

- `src/AgentSmith.Infrastructure/Services/Nuclei/NucleiSpawner.cs` — use IToolRunner
- `src/AgentSmith.Infrastructure/Services/Spectral/SpectralSpawner.cs` — use IToolRunner
- `src/AgentSmith.Infrastructure/Extensions/ServiceCollectionExtensions.cs` — DI
- `config/agentsmith.yml` + `config/agentsmith.example.yml`

## Definition of Done

- [ ] IToolRunner interface with ToolRunRequest/ToolResult
- [ ] DockerToolRunner (migrated from current DockerContainerRunner logic)
- [ ] ProcessToolRunner (direct process execution)
- [ ] K8sJobToolRunner (ephemeral K8s Jobs)
- [ ] NucleiSpawner + SpectralSpawner use IToolRunner
- [ ] Configuration selects implementation
- [ ] Auto-detection fallback (K8s cluster → k8s, docker socket → docker, else process)
- [ ] Tests for each runner
- [ ] docker-compose.yml updated
- [ ] Documentation in README

## Dependencies

- None (standalone, replaces current DockerContainerRunner for tools)
