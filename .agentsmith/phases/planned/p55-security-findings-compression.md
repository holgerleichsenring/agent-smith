# Phase 55: Security Findings Compression

## Goal

New `CompressSecurityFindingsCommand` that runs after the three tool commands
and before triage. Compresses 271+ raw findings into a compact, skill-aware
format that reduces token usage by 70-80% without losing signal.

## Motivation

The security-scan pipeline produces hundreds of raw findings from three sources:
- StaticPatternScan: 271 findings (file, line, pattern, severity, matched text)
- GitHistoryScan: 15 secrets (commit, file, pattern, severity)
- DependencyAudit: 1+ vulnerabilities (package, version, CVE, severity)

All of these are currently passed as raw context to every LLM skill round.
Six specialists each receive the full set — that's 6x the token cost for
the same data. The findings are repetitive (same pattern in many files)
and most skills only care about their specific category.

This is the same problem api-scan solved with swagger compression (p48).
The solution is the same pattern: a dedicated compression handler.

## Architecture

### New Pipeline Command

```
StaticPatternScan → GitHistoryScan → DependencyAudit
    → CompressSecurityFindings ← NEW
        → LoadSkills → AnalyzeCode → SecurityTriage → SkillRounds
```

### CompressSecurityFindingsHandler

Reads from PipelineContext:
- `ContextKeys.StaticScanResult` (StaticScanResult)
- `ContextKeys.GitHistoryScanResult` (GitHistoryScanResult)
- `ContextKeys.DependencyAuditResult` (DependencyAuditResult)

Produces two outputs:

#### 1. Compressed Summary (for all skills)

Written to `ContextKeys.SecurityFindingsSummary` — a compact markdown string
that every skill receives as baseline context:

```markdown
## Security Scan Summary

### Static Pattern Scan
271 findings in 647 files (91 patterns, 805ms)

| Category | Critical | High | Medium | Low | Total |
|----------|----------|------|--------|-----|-------|
| secrets | 0 | 12 | 5 | 3 | 20 |
| injection | 0 | 8 | 15 | 2 | 25 |
| ssrf | 0 | 3 | 7 | 0 | 10 |
| config | 2 | 5 | 8 | 1 | 16 |
| compliance | 0 | 4 | 12 | 6 | 22 |
| ai-security | 1 | 6 | 9 | 2 | 18 |

### Git History Secrets
15 secrets in 177 commits
- 3 CRITICAL (history-only, believed deleted)
- 12 HIGH (still in working tree)

### Dependency Audit (dotnet)
1 vulnerability: KubernetesClient 16.0.7 (moderate, GHSA-w7r3-mgwf-4mqq)
```

This is ~500 tokens instead of ~5000 for the raw findings.

#### 2. Skill-Specific Finding Slices (per category)

Written to `ContextKeys.SecurityFindingsByCategory` — a dictionary mapping
category names to their detailed findings. Each skill round only receives
the slice relevant to that skill:

```csharp
Dictionary<string, string> slices = {
    "secrets" → "Top 10 findings + file list for remaining 10",
    "injection" → "Top 10 findings + file list for remaining 15",
    "config" → "All 16 findings (under threshold, no compression)",
    "compliance" → "Top 10 findings + file list for remaining 12",
    "ai-security" → "Top 10 findings + file list for remaining 8",
    "ssrf" → "All 10 findings (under threshold, no compression)",
    "dependencies" → "All 1 CVE with full details",
    "history" → "All 15 secrets with masked values",
}
```

### Compression Rules

1. **Category grouping** — findings grouped by pattern category
2. **Deduplication** — same pattern in same file → keep first, count rest
3. **Top-N detail** — show full details for top 10 highest-severity findings per category
4. **Remainder summary** — remaining findings as "filename:line" list (compact)
5. **Threshold** — categories with ≤15 findings get full detail, no compression
6. **Matched text truncation** — matched text capped at 80 chars
7. **History findings** — always included in full (typically <20, all high value)
8. **Dependency findings** — always included in full (typically <10)

### SecuritySkillRoundHandler Update

The handler currently provides code analysis as domain context. Updated to also
include:
- `SecurityFindingsSummary` (always — compact overview)
- Skill-specific finding slice from `SecurityFindingsByCategory` based on
  a mapping from skill name to relevant categories:

```csharp
private static readonly Dictionary<string, string[]> SkillCategories = new()
{
    ["secrets-detector"] = ["secrets", "history"],
    ["injection-checker"] = ["injection", "ssrf"],
    ["auth-reviewer"] = ["secrets", "injection"],
    ["config-auditor"] = ["config"],
    ["supply-chain-auditor"] = ["dependencies"],
    ["compliance-checker"] = ["compliance"],
    ["ai-security-reviewer"] = ["ai-security"],
    ["vuln-analyst"] = ["secrets", "injection", "ssrf", "config", "dependencies"],
    ["false-positive-filter"] = ["secrets", "injection", "ssrf", "config", "compliance", "ai-security"],
};
```

Skills only receive findings they can act on. The `vuln-analyst` and
`false-positive-filter` get a broader view since they're generalists.

### Token Savings Estimate

Current (uncompressed):
- 6 skill rounds × ~5000 tokens findings context = ~30,000 input tokens

After compression:
- 6 skill rounds × ~500 tokens summary + ~800 tokens relevant slice = ~7,800 input tokens

**Savings: ~74%** on findings context, translating to ~$0.07 per scan at Sonnet pricing.

## Files to Create

- `AgentSmith.Contracts/Commands/CommandNames.cs` — add CompressSecurityFindings
- `AgentSmith.Application/Models/CompressSecurityFindingsContext.cs`
- `AgentSmith.Application/Services/Handlers/CompressSecurityFindingsHandler.cs`
- `AgentSmith.Application/Services/Builders/CompressSecurityFindingsContextBuilder.cs`
- `AgentSmith.Application/Services/SecurityFindingsCompressor.cs` — compression logic

## Files to Modify

- `AgentSmith.Contracts/Commands/PipelinePresets.cs` — insert command in security-scan
- `AgentSmith.Contracts/Commands/ContextKeys.cs` — add new keys
- `AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` — register handler
- `AgentSmith.Application/Services/Handlers/SecuritySkillRoundHandler.cs` — inject compressed findings

## Definition of Done

- [ ] CompressSecurityFindingsHandler groups, deduplicates, and compresses findings
- [ ] Summary produces severity-by-category table
- [ ] Skill-specific slices map skills to relevant categories
- [ ] Top-N detail with remainder summary for large categories
- [ ] SecuritySkillRoundHandler injects summary + relevant slice per skill
- [ ] Token usage reduced by 70%+ on findings context
- [ ] Threshold-based: small categories pass through uncompressed
- [ ] All existing tests pass
- [ ] New tests for compression logic

## Dependencies

- p54 (security scan expansion) — must be complete
