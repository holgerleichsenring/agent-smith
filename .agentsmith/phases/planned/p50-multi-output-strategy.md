# Phase 50: Multi-Output Strategy

## Goal

`--output` accepts comma-separated values. Multiple `IOutputStrategy`
implementations run for a single scan. Output files go to a configurable
directory via `--output-dir`.

## CLI

```bash
agent-smith api-scan \
  --swagger ./swagger.json \
  --target https://api.example.com \
  --output console,markdown,sarif \
  --output-dir ./security-report
```

- `--output-dir` default: `./agentsmith-output/` locally, `/output/` in Docker.
- When running in Docker the caller mounts the output directory:

```yaml
volumes:
  - $(Build.ArtifactStagingDirectory)/security:/output
```

## Changes

- `--output` parsed as `string[]` instead of `string`
- `DeliverFindingsHandler` resolves all requested strategies and executes
  them — console always goes to stdout, file-based strategies write to
  `--output-dir`
- `--output-dir` passed through `PipelineContext`
- File naming: `findings.md`, `findings.sarif` — no timestamps, predictable
  names for pipeline artifact publishing
- Console output unchanged — always stdout regardless of `--output-dir`

## Files to Modify

- `src/AgentSmith.Host/Commands/ApiScanCommand.cs` — parse `--output` as
  comma-separated, add `--output-dir` option
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add `OutputDir`
- `src/AgentSmith.Application/Services/Handlers/DeliverFindingsHandler.cs` —
  iterate over strategies, pass output dir
- `src/AgentSmith.Infrastructure/Services/Output/MarkdownOutputStrategy.cs` —
  write to output dir
- `src/AgentSmith.Infrastructure/Services/Output/SarifOutputStrategy.cs` —
  write to output dir
- `src/AgentSmith.Application/Models/ApiSecurityContexts.cs` — update
  `DeliverFindingsContext` if needed

## Dependencies

- p43c (IOutputStrategy, SarifOutputStrategy, MarkdownOutputStrategy)

## Definition of Done

- [ ] `--output console,markdown,sarif` runs all three strategies
- [ ] `--output-dir` controls file output location
- [ ] Default paths work locally and in Docker
- [ ] Console always stdout, never a file
- [ ] `dotnet build` + `dotnet test` clean
