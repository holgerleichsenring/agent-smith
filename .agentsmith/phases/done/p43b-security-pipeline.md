# Phase 43b: Security Pipeline — CLI, Docker, DeliverOutputCommand

## Goal

A new pipeline type `security-scan` that analyzes code changes for security
vulnerabilities. This phase implements the CLI verb, the pipeline preset,
security skills, `PrDiffProvider`, and the `DeliverOutputCommand` (which also
unblocks the legal-analysis pipeline).

Webhook triggers (GitHub PR labels, GitLab MR, AzDO) are deferred to p43e.

---

## Breaking Change: CLI Subcommands

**Current**: `agent-smith "fix #42 in my-api"`
**New**: `agent-smith run "fix #42 in my-api"`

Refactor `Program.cs` from single root command to `System.CommandLine` subcommands:

```
agent-smith run "fix #42 in my-api"           # existing ticket pipeline
agent-smith run "fix #42 in my-api" --dry-run  # existing dry run
agent-smith security-scan --repo . --output sarif
agent-smith server --port 8081                 # existing webhook server
```

**Migration note**: Existing wrapper scripts and `docker-compose.yml` command
entries must be updated. Document in CHANGELOG.

### `security-scan` Subcommand

```
agent-smith security-scan --repo ./my-api --output sarif
agent-smith security-scan --repo . --pr 42 --output markdown
agent-smith security-scan --repo . --pr $CI_MERGE_REQUEST_IID --output sarif
```

Parameters:
- `--repo` path or URL (required)
- `--pr` PR/MR number (optional — diff only; if absent, full repo scan)
- `--output` `sarif` | `markdown` | `console` (default: `console`)
- `--project` project name from config (optional, for multi-project configs)

---

## IPrDiffProvider — New Interface in Contracts

```csharp
// src/AgentSmith.Contracts/Providers/IPrDiffProvider.cs
public interface IPrDiffProvider
{
    Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken ct);
}

public sealed record PrDiff(
    string BaseSha,
    string HeadSha,
    IReadOnlyList<ChangedFile> Files);

public sealed record ChangedFile(
    string Path,
    string Patch,
    ChangeKind Kind);

public enum ChangeKind { Added, Modified, Deleted }
```

Implementations in Infrastructure:
- `GitHubPrDiffProvider` — `GET /repos/{owner}/{repo}/pulls/{number}/files`
- `GitLabPrDiffProvider` — `GET /projects/{id}/merge_requests/{iid}/diffs`
- `AzureDevOpsPrDiffProvider` — DevOps REST API

Each provider also implements `ISourceProvider` (for full checkout) +
`IPrDiffProvider` (for diff-only). `ProviderRegistry<IPrDiffProvider>` from
p40 applies.

When `--pr` is absent, falls back to `ISourceProvider.CheckoutAsync()`.

---

## DeliverOutputCommand — First Step

Implement `DeliverOutputCommand` handler as the generalized output delivery step.
This replaces the hardcoded file-based `DeliverOutputHandler` from Phase 42.

The handler receives `IOutputStrategy` via DI (keyed services, .NET 8).
`--output sarif` resolves `SarifOutputStrategy`, etc.

```csharp
// Contracts
public interface IOutputStrategy
{
    string StrategyType { get; }
    Task DeliverAsync(OutputContext context, CancellationToken ct);
}

public sealed record OutputContext(
    string ProjectName,
    string? PrIdentifier,
    IReadOnlyList<Finding> Findings,
    string? ReportMarkdown,
    PipelineContext Pipeline);
```

In p43b, implement only `ConsoleOutputStrategy` (stdout).
`SarifOutputStrategy` and `MarkdownOutputStrategy` come in p43c.
`LocalFileOutputStrategy` is the refactored `DeliverOutputHandler` from p42.

---

## Security Skills (`config/skills/security/`)

```
config/skills/security/
  security-principles.md
  vuln-analyst.yaml
  false-positive-filter.yaml
  auth-reviewer.yaml
  injection-checker.yaml
  secrets-detector.yaml
```

Skill YAML follows established format (triggers, rules, convergence_criteria).

`security-principles.md` — scope, exclusions:
- No DoS, no log spoofing, no theoretical race conditions
- No memory safety in managed languages
- No test-only files, no SSRF path-only
- Confidence < 8 → discarded by false-positive-filter

### Triage Skill Selection

Based on diff content:
- Auth-related files changed → include `auth-reviewer`
- DB access changed → include `injection-checker`
- Config/env files changed → include `secrets-detector`
- Always: `vuln-analyst` + `false-positive-filter`

---

## Pipeline Preset

```csharp
public static readonly IReadOnlyList<string> SecurityScan =
[
    CommandNames.AcquireSource,
    CommandNames.LoadDomainRules,
    CommandNames.Triage,
    // [SkillRounds inserted by Triage]
    CommandNames.ConvergenceCheck,
    CommandNames.CompileDiscussion,
    CommandNames.DeliverOutput,
];
```

Added to `PipelinePresets.cs` as `["security-scan"] = SecurityScan`.

---

## Config: Opt-in Per Project

```yaml
projects:
  my-api:
    security_scan: on_label    # on_pr | on_label | never | scheduled
    security_scan_schedule: "0 3 * * 1"  # cron, only when scheduled
```

---

## Files to Create

- `src/AgentSmith.Contracts/Providers/IPrDiffProvider.cs` — interface + records
- `src/AgentSmith.Contracts/Services/IOutputStrategy.cs` — interface + OutputContext
- `src/AgentSmith.Infrastructure/Services/Providers/Source/GitHubPrDiffProvider.cs`
- `src/AgentSmith.Infrastructure/Services/Providers/Source/GitLabPrDiffProvider.cs`
- `src/AgentSmith.Infrastructure/Services/Providers/Source/AzureDevOpsPrDiffProvider.cs`
- `src/AgentSmith.Infrastructure/Services/Output/ConsoleOutputStrategy.cs`
- `config/skills/security/*.yaml` + `security-principles.md` (6 files)
- Tests for PrDiffProviders (mocked HTTP), Triage skill selection

## Files to Modify

- `src/AgentSmith.Cli/Program.cs` — refactor to subcommands (**breaking change**)
- `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` — add `security-scan`
- `src/AgentSmith.Contracts/Commands/CommandNames.cs` — verify AcquireSource, DeliverOutput exist
- `src/AgentSmith.Application/Services/Handlers/DeliverOutputHandler.cs` — refactor to use IOutputStrategy
- `docker-compose.yml` — update command entries for `run` subcommand

---

## Definition of Done

- [ ] `agent-smith security-scan` CLI verb works locally
- [ ] `agent-smith run` replaces root command (breaking change documented)
- [ ] `docker run holgerleichsenring/agent-smith security-scan --repo . --output console`
- [ ] `IPrDiffProvider` implementations for GitHub, GitLab, AzDO
- [ ] Full repo scan when no `--pr` specified
- [ ] `IOutputStrategy` interface + `ConsoleOutputStrategy`
- [ ] `DeliverOutputCommand` uses `IOutputStrategy` (keyed services)
- [ ] Security skills loaded, Triage selects relevant subset
- [ ] `security_scan` config respected (never = no trigger registered)
- [ ] Unit tests: PrDiffProviders (mocked HTTP), Triage skill selection
- [ ] Integration test: CLI end-to-end with local repo
