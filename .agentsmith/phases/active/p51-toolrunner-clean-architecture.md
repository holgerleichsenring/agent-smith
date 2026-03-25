# Phase 51: ToolRunner Clean Architecture

## Problem

The current IToolRunner implementation grew through trial-and-error debugging
of Docker-from-Docker issues. The result works but is fragile:

- Spawners hardcode container-internal paths (`/tmp/targets.txt`, `/tmp/results.jsonl`)
- ProcessToolRunner rewrites `/tmp/` references back to local paths — brittle string replacement
- DockerToolRunner builds tar archives with manual byte manipulation
- Output strategies have `IsWritable()` fallback chains instead of clear paths
- No separation between "what files the tool needs" and "where they live"

## Goal

Clean separation of concerns:

1. **Spawners** define logical I/O: "this tool needs these input files and produces this output file"
2. **ToolRunRequest** carries logical file descriptions, not container paths
3. **Each Runner** translates logical I/O to its execution model internally
4. **Output directory** resolved once at pipeline level, not per-strategy

## Design

### ToolRunRequest (revised)

```csharp
public sealed record ToolRunRequest(
    string Tool,
    IReadOnlyList<string> Arguments,        // tool-specific args, NO path references
    Dictionary<string, string>? InputFiles,  // logical name → content
    string? OutputFileName,                  // logical name of expected output
    Dictionary<string, string>? ExtraHosts,
    int TimeoutSeconds = 300);
```

Arguments contain **placeholders** like `{input}` and `{output}` that each runner
resolves to its own paths:

```csharp
// NucleiSpawner builds:
arguments: ["-list", "{input}/targets.txt", "-output", "{output}/results.jsonl", ...]

// DockerToolRunner resolves to:
["-list", "/work/targets.txt", "-output", "/work/results.jsonl"]
// and copies InputFiles to /work/, reads OutputFile from /work/

// ProcessToolRunner resolves to:
["-list", "/tmp/nuclei-abc123/targets.txt", "-output", "/tmp/nuclei-abc123/results.jsonl"]
```

### Runner responsibilities

| Concern | DockerToolRunner | ProcessToolRunner | K8sJobToolRunner |
|---------|-----------------|-------------------|-----------------|
| Working dir | Creates `/work` in container via docker cp | Local temp dir | K8s emptyDir volume |
| Input files | `docker cp` tar to `/work` | Write to temp dir | ConfigMap or init container |
| Output file | `docker cp` from `/work/{name}` | Read from temp dir | kubectl cp or logs |
| Path resolution | `{input}` → `/work`, `{output}` → `/work` | `{input}` → tempDir, `{output}` → tempDir | `{input}` → `/work`, `{output}` → `/work` |
| Cleanup | Remove container | Delete temp dir | K8s TTL |

### OutputDir resolution (simplified)

Resolved once in `DeliverFindingsHandler`, passed to strategies as a concrete
writable path. No per-strategy fallback chains:

```csharp
var outputDir = ResolveOutputDir(context.OutputDir);
// Strategies receive outputDir as parameter, don't resolve it themselves
```

## Changes

### Files to modify

- `src/AgentSmith.Contracts/Providers/IToolRunner.cs` — add `{input}`/`{output}` placeholder contract
- `src/AgentSmith.Infrastructure/Services/Tools/DockerToolRunner.cs` — use `/work`, clean tar impl, resolve placeholders
- `src/AgentSmith.Infrastructure/Services/Tools/ProcessToolRunner.cs` — resolve placeholders to temp dir
- `src/AgentSmith.Infrastructure/Services/Nuclei/NucleiSpawner.cs` — use `{input}`/`{output}` placeholders
- `src/AgentSmith.Infrastructure/Services/Spectral/SpectralSpawner.cs` — use `{input}`/`{output}` placeholders
- `src/AgentSmith.Infrastructure/Services/Output/MarkdownOutputStrategy.cs` — receive outputDir, remove IsWritable
- `src/AgentSmith.Infrastructure/Services/Output/SarifOutputStrategy.cs` — receive outputDir, remove IsWritable
- `src/AgentSmith.Application/Services/Handlers/DeliverFindingsHandler.cs` — resolve outputDir once
- `src/AgentSmith.Contracts/Services/IOutputStrategy.cs` — add outputDir to OutputContext or DeliverAsync

### Tests

- DockerToolRunner: placeholder resolution, tar creation, file round-trip
- ProcessToolRunner: placeholder resolution, temp dir lifecycle
- NucleiSpawner/SpectralSpawner: verify no hardcoded paths in arguments
- OutputStrategy: verify no fallback logic, receives concrete path

## Definition of Done

- [ ] No hardcoded container paths in Spawners
- [ ] `{input}`/`{output}` placeholders resolved by each runner
- [ ] DockerToolRunner uses `/work` with clean docker cp
- [ ] ProcessToolRunner resolves to temp dir cleanly
- [ ] OutputDir resolved once in handler, not per-strategy
- [ ] No `IsWritable()` fallback chains
- [ ] All existing tests pass
- [ ] New tests for placeholder resolution
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- p49 (IToolRunner abstraction)
- p50 (multi-output strategy)
