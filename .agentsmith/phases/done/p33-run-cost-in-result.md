# Phase 33: Token Usage & Cost Data in result.md

## Problem

`RunCostSummary` is computed by `CostTracker` during agent execution but only logged via `ILogger`.
It never reaches `WriteRunResultHandler` — cost data is lost after the run completes.
The `result.md` currently contains only ticket, date, type, changed files, and summary.

## Goal

Persist token usage and cost data in `result.md` as YAML frontmatter so it is:
- **Human-readable** in GitHub PR / Markdown viewers
- **Machine-parseable** via any YAML library or `yq`
- **Aggregatable** across all `runs/*/result.md` for reporting

## Target Format

```markdown
# r01: Add filter parameter to GET /todos endpoint

---
ticket: "#58 — Add filter parameter to GET /todos endpoint"
date: 2026-02-24
result: success
type: feat
duration_seconds: 145
tokens:
  input: 42350
  output: 8120
  cache_read: 18200
  total: 68670
cost:
  total_usd: 0.38
  phases:
    scout:
      model: claude-haiku-4-5-20251001
      input: 4200
      output: 1800
      cache_read: 2100
      turns: 1
      usd: 0.02
    primary:
      model: claude-sonnet-4-20250514
      input: 38150
      output: 6320
      cache_read: 16100
      turns: 6
      usd: 0.36
---

## Changed Files
- [Create] Services/TodoStore.cs
- [Modify] Program.cs

## Summary
Add optional status query parameter...
```

## Steps

### Step 1: Move cost models to Contracts

**Move** `RunCostSummary` and `PhaseCost` from `Infrastructure/Models/` to `Contracts/Models/`.
These are plain DTOs needed by Application layer (`WriteRunResultHandler`).

Also move `TokenUsageSummary` to Contracts — it is referenced by `RunCostSummary` consumers.

Delete the old files in Infrastructure/Models/.

### Step 2: Store RunCostSummary in PipelineContext

**Edit** `ClaudeAgentProvider.ExecutePlanAsync()`:
- After `LogCostSummary()`, store the summary in pipeline context:
  `pipeline.Set(ContextKeys.RunCostSummary, costSummary)`
- Add `ContextKeys.RunCostSummary` constant

**Edit** `ClaudeAgentProvider.LogCostSummary()`:
- Return `RunCostSummary?` instead of void, so the caller can store it

The `PipelineContext` is available via the handler chain — check how it flows from
`AgenticExecuteHandler` to `WriteRunResultHandler`.

### Step 3: Add duration tracking

**Edit** `AgenticExecuteHandler` (or `ClaudeAgentProvider`):
- Record `Stopwatch` start/stop around the agentic execution
- Store `ContextKeys.RunDurationSeconds` (int) in PipelineContext

### Step 4: Extend WriteRunResultHandler

**Edit** `WriteRunResultHandler.WriteResultAsync()`:
- Read `RunCostSummary?` from PipelineContext (optional — may be null if cost tracking disabled)
- Read `RunDurationSeconds?` from PipelineContext
- Generate YAML frontmatter block between `---` markers
- Keep existing Changed Files and Summary sections below frontmatter

The frontmatter should be valid YAML parseable by YamlDotNet or any standard YAML parser.

### Step 5: Tests

- `WriteRunResultHandlerTests`: verify result.md contains frontmatter with cost data when RunCostSummary is present
- `WriteRunResultHandlerTests`: verify result.md works without cost data (graceful fallback)
- Verify YAML frontmatter is parseable (round-trip test with YamlDotNet)

### Step 6: Build + Test

- `dotnet build` — 0 errors
- `dotnet test` — all tests pass

## File Summary

| Action | File |
|--------|------|
| Move | `RunCostSummary.cs`, `PhaseCost` → `Contracts/Models/` |
| Move | `TokenUsageSummary.cs` → `Contracts/Models/` |
| Edit | `ContextKeys.cs` — add `RunCostSummary`, `RunDurationSeconds` |
| Edit | `ClaudeAgentProvider.cs` — store cost summary in pipeline context |
| Edit | `AgenticExecuteHandler.cs` — duration tracking via Stopwatch |
| Edit | `WriteRunResultHandler.cs` — YAML frontmatter with cost/token/duration data |
| Edit | `WriteRunResultHandlerTests.cs` — new tests for cost data in result.md |
