# Dry-Run Summary — All Pipelines

**Date:** 2026-04-23
**Config:** `config/agentsmith.yml`
**Binary:** `src/AgentSmith.Cli/bin/Debug/net8.0/AgentSmith.Cli`

All 10 pipelines resolved successfully from presets. No execution occurred.

## Overview

| Command | Pipeline | Project used | Steps |
|---|---|---|---|
| `fix` | `fix-bug` | `agent-smith` | 14 |
| `feature` | `add-feature` | `todo-list` | 16 |
| `init` | `init-project` | `agent-smith` | 3 |
| `mad` | `mad-discussion` | `mad-discussion` | 9 |
| `security-scan` | `security-scan` | `agent-smith-security` | 17 |
| `api-scan` | `api-security-scan` | `agent-smith-api-security-claude` | 11 |
| `legal` | `legal-analysis` | `legal` (default) | 7 |
| `security-trend` | *(utility)* | `agent-smith-security` | 3 |
| `compile-wiki` | *(utility)* | `agent-smith` | 3 |
| `autonomous` | `autonomous` | `agent-smith` | 11 |

---

## Pipeline Details

### fix — `fix-bug` (14 steps)
`FetchTicketCommand` → `CheckoutSourceCommand` → `BootstrapProjectCommand` → `LoadCodeMapCommand` → `LoadDomainRulesCommand` → `LoadContextCommand` → `AnalyzeCodeCommand` → `TriageCommand` → `GeneratePlanCommand` → `ApprovalCommand` → `AgenticExecuteCommand` → `TestCommand` → `WriteRunResultCommand` → `CommitAndPRCommand`

### feature — `add-feature` (16 steps)
Adds `GenerateTestsCommand` and `GenerateDocsCommand` on top of the fix-bug flow.

### init — `init-project` (3 steps)
`CheckoutSourceCommand` → `BootstrapProjectCommand` → `InitCommitCommand`

### mad — `mad-discussion` (9 steps)
`FetchTicketCommand` → `CheckoutSourceCommand` → `BootstrapProjectCommand` → `LoadContextCommand` → `TriageCommand` → `ConvergenceCheckCommand` → `CompileDiscussionCommand` → `WriteRunResultCommand` → `CommitAndPRCommand`

### security-scan — `security-scan` (17 steps)
`CheckoutSourceCommand` → `BootstrapProjectCommand` → `LoadDomainRulesCommand` → `StaticPatternScanCommand` → `GitHistoryScanCommand` → `DependencyAuditCommand` → `SecurityTrendCommand` → `CompressSecurityFindingsCommand` → `LoadSkillsCommand` → `AnalyzeCodeCommand` → `SecurityTriageCommand` → `ConvergenceCheckCommand` → `CompileDiscussionCommand` → `ExtractFindingsCommand` → `DeliverFindingsCommand` → `SecuritySnapshotWriteCommand` → `SpawnFixCommand`

### api-scan — `api-security-scan` (11 steps)
`LoadSwaggerCommand` → `SessionSetupCommand` → `SpawnNucleiCommand` → `SpawnSpectralCommand` → `SpawnZapCommand` → `CompressApiScanFindingsCommand` → `LoadSkillsCommand` → `ApiSecurityTriageCommand` → `ConvergenceCheckCommand` → `CompileFindingsCommand` → `DeliverFindingsCommand`

> Hinweis: Lief im Passive-Mode (keine Credentials).

### legal — `legal-analysis` (7 steps)
`AcquireSourceCommand` → `BootstrapDocumentCommand` → `LoadDomainRulesCommand` → `TriageCommand` → `ConvergenceCheckCommand` → `CompileDiscussionCommand` → `DeliverOutputCommand`

### security-trend (utility, 3 steps)
- Load security snapshots from `.agentsmith/security/`
- Compute trend analysis (critical/high history, costs)
- Print trend report to console

### compile-wiki (utility, 3 steps)
- Scan `.agentsmith/runs/` for new runs
- Compile run results into knowledge base wiki
- Update `.last-compiled` marker

### autonomous — `autonomous` (11 steps)
`CheckoutSourceCommand` → `BootstrapProjectCommand` → `LoadContextCommand` → `LoadCodeMapCommand` → `LoadVisionCommand` → `LoadRunsCommand` → `TriageCommand` → `ConvergenceCheckCommand` → `CompileDiscussionCommand` → `WriteTicketsCommand` → `WriteRunResultCommand`

---

## Ergebnis

- ✅ 10/10 Pipelines aus den Presets aufgelöst
- ✅ Keine Fehler, keine fehlenden Commands
- ⚠️ `api-scan` fiel ohne `--admin-user` in den Passive-Mode (erwartet)
