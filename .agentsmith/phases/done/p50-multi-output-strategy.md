# Phase 50: Multi-Output Strategy

## Goal

`--output` accepts comma-separated values. Multiple `IOutputStrategy`
implementations run for a single scan. Output files go to a configurable
directory via `--output-dir`. A new `summary` output type provides a clean,
readable findings report without skill discussion noise.

## CLI

```bash
agent-smith api-scan \
  --swagger ./swagger.json \
  --target https://api.example.com \
  --output console,summary,markdown,sarif \
  --output-dir ./security-report
```

## Output Types

- **console** — current behavior, full pipeline log including skill discussion
  rounds, always stdout
- **summary** — new: clean findings-only output, no skill discussion, no
  round-by-round noise. stdout. Format:

```
═══════════════════════════════════════
  Agent Smith — API Security Summary
═══════════════════════════════════════

CRITICAL (2)
  [API2:2023] Plaintext passcode in API response — confidence 9
  [API3:2023] Credential bundling in OktaProcessInfoResponse — confidence 10

HIGH (3)
  ...

MEDIUM (4)
  ...

Filtered: 102 infrastructure findings excluded
Total: 9 retained from 111 analyzed
LLM calls: 5 · Tokens: 23854 · Cost: $0.11
═══════════════════════════════════════
```

- **markdown** — full report as `findings.md` in `--output-dir`
- **sarif** — `findings.sarif` in `--output-dir`

## Configuration

- `--output-dir` default: `./agentsmith-output/` locally, `/output/` in Docker.
- Caller mounts output directory:

```yaml
volumes:
  - $(Build.ArtifactStagingDirectory)/security:/output
```

## Console Log Readability

The existing console output stays as-is for debugging. But the default pipeline
usage should be `--output summary,markdown,sarif` — summary gives the clean
view, markdown gives the full report as artifact, sarif for tooling integration.
Document this as the recommended pipeline configuration in README.

## Changes

- `--output` parsed as `string[]` instead of `string`
- New `SummaryOutputStrategy` — renders only retained findings grouped by
  severity, plus cost line
- `DeliverFindingsHandler` resolves all requested strategies and executes them
- `--output-dir` passed through `PipelineContext`
- File naming: `findings.md`, `findings.sarif` — predictable names for
  pipeline artifact publishing
- Console and summary always stdout, never files

## Files to Create

- `src/AgentSmith.Infrastructure/Services/Output/SummaryOutputStrategy.cs`

## Files to Modify

- `src/AgentSmith.Cli/Commands/ApiScanCommand.cs` — parse `--output` as
  comma-separated, add `--output-dir` option
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add `OutputDir`
- `src/AgentSmith.Application/Services/Handlers/DeliverFindingsHandler.cs` —
  iterate over strategies, pass output dir
- `src/AgentSmith.Infrastructure/Services/Output/MarkdownOutputStrategy.cs` —
  write to output dir
- `src/AgentSmith.Infrastructure/Services/Output/SarifOutputStrategy.cs` —
  write to output dir
- `src/AgentSmith.Infrastructure/Extensions/ServiceCollectionExtensions.cs` —
  register SummaryOutputStrategy
- `src/AgentSmith.Application/Models/ApiSecurityContexts.cs` — update
  `DeliverFindingsContext` if needed
- README — document recommended `--output summary,markdown,sarif`

## Dependencies

- p43c (IOutputStrategy, SarifOutputStrategy, MarkdownOutputStrategy)

## Definition of Done

- [ ] `--output console,summary,markdown,sarif` runs all four strategies
- [ ] `summary` shows only retained findings grouped by severity — no skill discussion
- [ ] Cost line visible in summary output
- [ ] `--output-dir` controls file output location
- [ ] Default paths work locally and in Docker
- [ ] README documents recommended pipeline configuration
- [ ] `dotnet build` + `dotnet test` clean
