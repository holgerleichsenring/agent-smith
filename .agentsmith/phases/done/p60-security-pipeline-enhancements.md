# Phase 60: Security Pipeline — ZAP, Auto-Fix & Git-Based Trend Analysis

## Goal

Three enhancements that take the security stack to the next level:

1. **DAST via OWASP ZAP** — dynamic testing of running applications
2. **Auto-Fix Pipeline** — Critical/High findings are submitted directly as fix PRs
3. **Git-based Trend Analysis** — security trend over time, without external database

No new infrastructure concept. Everything builds on `IToolRunner`, `IOutputStrategy`,
`runs/`, `result.md`, and Git.

---

## Enhancement 1: DAST via OWASP ZAP

### Why ZAP

Nuclei finds known CVEs in running APIs. ZAP does something different:
it simulates an attacker probing the app from outside — XSS, CSRF,
auth bypass, session handling, header misconfiguration. Things that are
invisible in source code and only appear at runtime.

```
Today:      StaticPatternScan + GitHistory + DependencyAudit + Nuclei → LLM Skills

With ZAP:   + ZAP Baseline Scan (Web App Runtime) → LLM Skills
```

### SpawnZapCommand

Exact same pattern as SpawnNucleiCommand. Uses `docker cp` (not volume mounts)
to comply with Docker-from-Docker architecture:

```csharp
// 1. Start container without volume mount
// 2. docker cp swagger.json → container (if api-scan)
// 3. Execute scan
// 4. docker cp → output from container
// 5. Stop container
```

Scan types (configurable):

| Type | Duration | What it finds |
|---|---|---|
| baseline | ~2 min | Headers, TLS, passive findings — default |
| full-scan | ~10 min | + active injection tests — staging only! |
| api-scan | ~5 min | REST API specific with OpenAPI spec |

```yaml
# agentsmith.yml
projects:
  my-api:
    dast:
      enabled: true
      target: https://staging.my-api.example.com
      scan_type: baseline
      auth:
        type: bearer
        token_env: DAST_TOKEN
```

### New LLM Skills

**dast-analyst.yaml** — correlates ZAP findings with StaticPatternScan,
filters auth-protected false positives, OWASP Top 10 mapping.

**dast-false-positive-filter.yaml** — ZAP-specific:
known FP patterns, discard confidence < Medium.

---

## Enhancement 2: Auto-Fix Pipeline

### The Differentiator

No other security tool fixes vulnerabilities automatically.
They all find them. Agent Smith can fix them directly.

```
Today:
  security-scan → findings → SARIF → human reads → human creates ticket → fix-bug pipeline

With p60:
  security-scan → Critical/High findings → SpawnFixCommand → fix-bug pipeline → PR
                                               ↑
                               (with p58 Dialogue: confirmation first)
```

### SpawnFixCommand

Comes after DeliverFindings. Reads Critical/High findings from PipelineContext,
groups by file + category, starts a fix job per group:

```csharp
// Grouping: same file + same category = one fix job
var groups = fixable
    .GroupBy(f => (f.FilePath!, f.Category))
    .Take(config.MaxConcurrent);

foreach (var group in groups)
{
    await jobEnqueuer.EnqueueFixAsync(new SecurityFixRequest(
        group.Key.FilePath,
        group.Select(f => f.ToFixDescription()).ToList(),
        repoFullName, baseBranch), ct);
}
```

Spawns **separate K8s jobs/containers** via existing `IJobSpawner`.
Rationale: auto-fix can take minutes to 10+ minutes — must not block
the security scan. Max 3 parallel jobs per `max_concurrent_fixes` config.
The security scan job ends normally, fix jobs continue asynchronously.

The fix agent receives a security-specific system prompt:

```
## Security Fix Context
Fix the vulnerability conservatively — minimal code change.
Do NOT refactor unrelated code.
Add a comment explaining what was vulnerable and why the fix is correct.
Ensure existing tests still pass.

Findings:
{finding descriptions with file, line, CWE, description}
```

Branch naming: `security-fix/cwe-89-sql-injection-usercontroller`
PR title: `🔒 Security Fix: CWE-89 SQL Injection in UserController.cs`

### Config

```yaml
projects:
  my-api:
    auto_fix:
      enabled: false             # explicit opt-in
      severity_threshold: High   # Critical | High
      confirm_before_fix: true   # p58 dialogue step before spawn
      max_concurrent: 3
      excluded_patterns:
        - "**/*.generated.cs"
        - "**/Migrations/**"
```

---

## Enhancement 3: Git-Based Trend Analysis

### Philosophy

> "Why add persistence when you're already tracking everything in Git?"

Every security scan writes to runs/ with structured result.md.
Git is the database. git log + YAML frontmatter = time series.

### result.md Frontmatter Extension

WriteRunResultHandler gets a new block for security scans:

```yaml
---
ticket: security-scan
date: 2026-04-02T14:23:11Z
result: success
duration: 187
cost_usd: 0.23

security:
  findings_critical: 2
  findings_high: 7
  findings_medium: 14
  findings_retained: 9
  findings_auto_fixed: 3
  scan_types: [static, git-history, dependency, zap]
  new_since_last: 4
  resolved_since_last: 2
  top_categories: [secrets, injection, config]
---
```

### SecurityTrendCommand

Runs before DeliverFindings. Reads Git history of committed SARIF snapshots
from the default branch (not the PR branch):

```csharp
// Read SARIF snapshots committed on default branch via LibGit2Sharp
// Repository.Branches["main"] → Commit Tree → .agentsmith/security/*.sarif
var defaultBranch = repo.Branches["main"];
var history = ReadSecuritySnapshotsFromTree(defaultBranch.Tip.Tree)
    .OrderByDescending(r => r.Date)
    .ToList();

var trend = new SecurityTrend(
    NewFindings:      current.FindingsRetained - previous.FindingsRetained,
    ResolvedFindings: CountResolved(current, previous),
    CriticalDelta:    current.FindingsCritical - previous.FindingsCritical,
    HighDelta:        current.FindingsHigh - previous.FindingsHigh,
    TotalScans:       history.Count,
    AverageCost:      history.Average(r => r.CostUsd));
```

No API call, no Redis, no Docker. Only LibGit2Sharp (already a dependency)
reading committed snapshots from the Git tree. Works in any fresh checkout.

### Output in result.md

```markdown
## Security Trend

| Metric | Last Scan | This Scan | Delta |
|--------|-----------|-----------|-------|
| Critical | 3 | 2 | ✅ -1 |
| High | 8 | 7 | ✅ -1 |
| Retained | 11 | 9 | ✅ -2 |
| Auto-Fixed | 0 | 3 | ✅ +3 |
| Cost | $0.19 | $0.23 | ⚠️ +$0.04 |

Last scan: 2026-03-28 | Total scans: 7 | Avg cost: $0.21

📈 Trend: Improving — 2 findings resolved, 1 critical fewer
```

### Slack/PR Delivery

```
🔒 Security scan completed ✅

📊 9 findings (↓2 since last scan)
   Critical: 2 (↓1) | High: 7 (↓1)
   3 auto-fixes started

📈 Trend: 7 scans, steadily improving
Cost: $0.23 | PR: #156
```

### New CLI Subcommand: security-trend

```bash
agent-smith security-trend --project my-api

# Output:
Security Trend for my-api (7 scans)
══════════════════════════════════════
Last scan:         2026-04-01 ($0.23, 9 retained)
Best scan:         2026-03-01 (5 retained)
Worst scan:        2026-01-15 (23 retained)

Critical history:  23 → 18 → 12 → 8 → 5 → 3 → 2
High history:      -- → -- → 15 → 12 → 9 → 8 → 7

Avg cost/scan:     $0.21
Total invested:    $1.47
Auto-fixes:        8 PRs created, 6 merged
```

---

## Complete Pipeline Overview

```
security-scan (complete, p60):

BootstrapProject → LoadContext
  ├── StaticPatternScan      (p54)
  ├── GitHistoryScan         (p54)
  ├── DependencyAudit        (p54)
  └── SpawnZapCommand        (p60 NEW — when dast.enabled)
      └── SecurityTrendCommand  (p60 NEW — git-based, no API call)
          └── Triage
              └── [SkillRounds]
                  └── ConvergenceCheck
                      └── CompileFindings
                          └── ExtractFindings
                              └── DeliverFindings
                                  └── SpawnFixCommand  (p60 NEW — when auto_fix.enabled)
```

---

## Steps

| Step | What | Depends on |
|---|---|---|
| 1 | SecurityRunResult frontmatter extension | — |
| 2 | SecurityTrendCommand + record (reads from git tree) | Step 1 |
| 3 | Trend in result.md + delivery | Step 2 |
| 4 | security-trend CLI subcommand | Step 2 |
| 5 | ZapSpawner + ZapResult (docker cp pattern) | — |
| 6 | SpawnZapCommand + dast-* skills | Step 5 |
| 7 | SpawnFixCommand + SecurityFixRequest | p58 Steps 1+2 |
| 8 | Config + DI + Tests | All |

Steps 1–4 (trend) and 5–6 (ZAP) are fully parallelizable.

---

## Definition of Done

- [x] Security result.md frontmatter contains `security:` block
- [x] SecurityTrendHandler reads git-based history correctly (YAML snapshots)
- [x] Trend delta visible in result.md and delivery message
- [x] security-trend CLI subcommand outputs table
- [x] SpawnZapCommand starts ZAP container via IToolRunner (docker cp, no volume mounts)
- [x] ZAP baseline scan runs, ZapResult correctly parsed
- [x] dast-analyst skill evaluates ZAP findings in code context
- [x] ZAP + StaticPattern findings correlated (same file/category)
- [x] SpawnFixCommand writes fix requests for Critical/High findings (YAML to .agentsmith/security/fixes/)
- [x] confirm_before_fix triggers p58 dialogue step
- [x] Branch naming: security-fix/cwe-{id}-{slug}
- [x] auto_fix.enabled: false is default (explicit opt-in)
- [x] All existing security tests green
- [x] dotnet build zero warnings

---

## Dependencies

```
p54 + p55 + p56 (Security Stack) — complete
p58 (Dialogue) — only for SpawnFix confirm_before_fix

Steps 1–4:  immediately startable, no dependencies
Steps 5–6:  immediately startable, no dependencies
Step 7:     needs p58 Steps 1+2
```
