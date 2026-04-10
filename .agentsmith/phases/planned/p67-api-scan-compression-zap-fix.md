# Phase 67: API Scan — Finding Compression & ZAP Fix

## Goal

Two problems in the api-security-scan pipeline:

1. **No finding compression** — All 504 scanner findings (Nuclei + Spectral) go
   to every skill call unchanged. This costs 233k tokens / $0.82 per run.
   Security-scan solved this in p55 with category slicing. API-scan needs the same.

2. **ZAP `--auto` flag broken** — `ghcr.io/zaproxy/zaproxy:stable` no longer
   recognizes `--auto`. ZAP exits with code 3 after 98 seconds of doing nothing.
   DAST skills then run on empty input, wasting 2 LLM calls.

## Design

### Part 1: API Finding Compression

Follow the p55 pattern exactly:

**New command: `CompressApiScanFindingsCommand`**

Insert between `SpawnZapCommand` and `LoadSkillsCommand` in the pipeline.
Creates category slices from Nuclei, Spectral, and ZAP findings.

**Categories:**

| Category | Source | Target Skill |
|---|---|---|
| `auth` | Nuclei auth templates, Spectral security-scheme findings | auth-tester |
| `design` | Spectral schema/naming/structure findings | api-design-auditor |
| `runtime` | Nuclei all findings, ZAP all findings | dast-analyst |
| `all` | Everything merged | false-positive-filter, dast-false-positive-filter |

**New class: `ApiScanFindingsCompressor`**

```csharp
public static class ApiScanFindingsCompressor
{
    public static string BuildSummary(NucleiResult? nuclei, SpectralResult? spectral, ZapResult? zap);
    public static Dictionary<string, string> BuildCategorySlices(NucleiResult? nuclei, SpectralResult? spectral, ZapResult? zap);
    public static string GetSliceForSkill(string skillName, Dictionary<string, string> slices, List<string>? inputCategories);
}
```

Categorization logic for Spectral findings:
- `code` contains "security", "auth", "bearer", "oauth", "api-key" → `auth`
- `code` contains "schema", "type", "enum", "description", "summary", "naming", "operationId" → `design`
- Everything else → `design` (Spectral is primarily design-focused)

Categorization logic for Nuclei findings:
- `templateId` contains "auth", "jwt", "token", "session", "cookie" → `auth`
- All Nuclei findings also go to `runtime` (Nuclei is runtime-focused)

**Modify `ApiSkillRoundHandler.BuildDomainSection()`:**

Replace the raw finding formatting (lines 31-40) with:
1. Retrieve `ApiScanFindingsSummary` and `ApiScanFindingsByCategory` from pipeline
2. Use `GetSliceForSkill()` with the active skill name
3. If no category slices available, fall back to current behavior

**Update skill configs** (`config/skills/api-security/*/agentsmith.md`):

```yaml
# auth-tester
input_categories: auth

# api-design-auditor
input_categories: design

# dast-analyst
input_categories: runtime

# false-positive-filter
input_categories: auth, design, runtime

# dast-false-positive-filter
input_categories: runtime

# api-vuln-analyst (executor)
input_categories:  # empty — receives gate output only
```

**Expected token reduction:**

Current: 504 findings × 6 calls = ~270k chars finding data
After: each skill gets ~80-150 findings → ~60-80k chars total
Estimated: **~60% reduction** in input tokens, from $0.82 to ~$0.35 per run.

### Part 2: ZAP Fix

**Problem:** `--auto` flag removed in recent `zaproxy:stable` builds.

**Fix in `ZapSpawner.cs`:**

Remove `"--auto"` from all three argument builders (lines 68, 73, 82, 86).
The `--auto` flag was a convenience shortcut for headless mode — equivalent to
`-I` (non-interactive) which is already the default in Docker containers.

**Skip DAST skills on ZAP failure:**

In `SpawnZapHandler`, if ZAP exit code != 0, set a `ZapFailed` flag in
pipeline context. In `ApiSecurityTriageHandler`, if `ZapFailed` is set,
exclude `dast-analyst` and `dast-false-positive-filter` from the skill graph.
This saves 2 LLM calls when ZAP is broken or disabled.

## Files to Create

- `src/AgentSmith.Application/Services/ApiScanFindingsCompressor.cs`
- `src/AgentSmith.Application/Services/Handlers/CompressApiScanFindingsHandler.cs`
- `tests/AgentSmith.Tests/Services/ApiScanFindingsCompressorTests.cs`

## Files to Modify

- `src/AgentSmith.Infrastructure/Services/Zap/ZapSpawner.cs` — remove `--auto`
- `src/AgentSmith.Application/Services/Handlers/SpawnZapHandler.cs` — set ZapFailed flag
- `src/AgentSmith.Application/Services/Handlers/ApiSecurityTriageHandler.cs` — skip DAST skills on ZapFailed
- `src/AgentSmith.Application/Services/Handlers/ApiSkillRoundHandler.cs` — use category slices
- `src/AgentSmith.Application/Models/ApiSecurityContexts.cs` — add context keys
- `src/AgentSmith.Application/Services/PipelineFactory.cs` — insert CompressApiScanFindings command
- `config/skills/api-security/auth-tester/agentsmith.md` — add input_categories
- `config/skills/api-security/api-design-auditor/agentsmith.md` — add input_categories
- `config/skills/api-security/dast-analyst/agentsmith.md` — add input_categories
- `config/skills/api-security/false-positive-filter/agentsmith.md` — add input_categories
- `config/skills/api-security/dast-false-positive-filter/agentsmith.md` — add input_categories

## Definition of Done

- [ ] `--auto` removed from ZAP spawner, ZAP runs without exit code 3
- [ ] ZapFailed flag set on non-zero exit, DAST skills skipped
- [ ] `CompressApiScanFindingsCommand` inserted in pipeline between ZAP and LoadSkills
- [ ] `ApiScanFindingsCompressor` produces category slices (auth, design, runtime)
- [ ] `ApiSkillRoundHandler` uses category slices instead of raw findings
- [ ] All 5 API skill configs have `input_categories` set
- [ ] Unit tests for compressor (categorization, slicing, edge cases)
- [ ] API scan run completes with <150k input tokens (down from 233k)
- [ ] Finding quality unchanged — same 10 findings confirmed
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes

## Dependencies

- p48 (Swagger Context Compression) — already implemented, stays unchanged
- p55 (Security Findings Compression) — pattern to follow
- p64 (Typed Skill Orchestration) — provides the input_categories infrastructure
