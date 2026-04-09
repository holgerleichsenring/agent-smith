# Phase 63: Structured Finding Assessment

## Goal

Close the gap between LLM skill discussion and finding output. Skills currently
discuss compressed finding slices as free prose — the resulting ConsolidatedPlan
is never reconciled with the raw findings. Findings that skills identify as false
positives still appear in the final report. Findings above the compression
threshold are never seen by skills at all.

## Problem

The security-scan pipeline has two parallel paths that never converge:

```
StaticScan ──→ Compress (Top-10) ──→ Skills discuss ──→ ConsolidatedPlan (prose, ignored)
     │
     └──→ ExtractFindings ──→ DeliverFindings (325 raw findings, unfiltered)
```

Three concrete issues:

1. **Compression hides findings.** `SecurityFindingsCompressor` caps at 10
   findings per category. A category with 40 findings sends 10 to skills +
   a compact file list for the rest. Skills can't assess what they don't see.

2. **ConsolidatedPlan is unstructured.** It's a free-text numbered list from the
   convergence LLM call. No machine-readable status per finding, no confidence
   values, no explicit false-positive markings. Text-matching against it is fragile.

3. **ExtractFindingsHandler ignores the discussion.** It reads raw scan results
   directly and passes them through unfiltered. The entire LLM discussion
   (8 skills, convergence check) has zero effect on the output.

## Solution

### 1. Finding record gets ReviewStatus

`Finding.cs` gains a `ReviewStatus` field:

```csharp
public sealed record Finding(
    string Severity,
    string File,
    int StartLine,
    int? EndLine,
    string Title,
    string Description,
    int Confidence,
    string ReviewStatus = "not_reviewed");  // confirmed | false_positive | not_reviewed
```

### 2. Compression sends all findings to skills

Remove `TopNPerCategory = 10` cap in `SecurityFindingsCompressor`. All findings
go to their relevant skills as one-line entries. At ~40 findings per category
this adds ~3-4k tokens per skill call — acceptable.

`DetailThreshold` can stay as a formatting threshold (compact vs. verbose
rendering), but no findings are dropped.

### 3. Convergence produces structured assessments

`ConvergenceCheckHandler` consolidation prompt asks the LLM to output a
JSON array of assessed findings alongside the prose summary:

```json
{
  "summary": "...",
  "assessments": [
    { "file": "src/Foo.cs", "line": 42, "title": "...", "status": "false_positive", "reason": "test fixture, not production code" },
    { "file": "src/Bar.cs", "line": 10, "title": "...", "status": "confirmed", "reason": "real secret in source" }
  ]
}
```

Only findings that skills explicitly reviewed get an entry. Everything else
stays `not_reviewed`. The prose summary remains available for `CompileDiscussion`
and human-readable output.

Parsed assessments are stored as `List<FindingAssessment>` in the pipeline
context under `ContextKeys.FindingAssessments`.

### 4. ExtractFindingsHandler applies assessments

After collecting raw findings, reads `FindingAssessments` from the pipeline.
For each raw finding:

- Match by file + line (or file + title as fallback)
- If matched with `false_positive` → set `ReviewStatus = "false_positive"`
- If matched with `confirmed` → set `ReviewStatus = "confirmed"`
- If no match → stays `not_reviewed`

Filter: emit only `confirmed` and `not_reviewed`. Log filtered count.

## Files to Create

- `src/AgentSmith.Contracts/Models/FindingAssessment.cs` — assessment record

## Files to Modify

- `src/AgentSmith.Contracts/Models/Finding.cs` — add ReviewStatus
- `src/AgentSmith.Application/Services/SecurityFindingsCompressor.cs` — remove TopN cap
- `src/AgentSmith.Application/Services/Handlers/ConvergenceCheckHandler.cs` — structured consolidation prompt + parse assessments
- `src/AgentSmith.Application/Services/Handlers/ExtractFindingsHandler.cs` — apply assessments, filter false positives
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add FindingAssessments key

## Dependencies

- None — builds on existing pipeline, no new commands or steps

## Definition of Done

- [ ] Finding record has ReviewStatus field
- [ ] All findings reach skills (no Top-10 cap)
- [ ] Convergence produces structured JSON assessments
- [ ] ExtractFindingsHandler applies assessments and filters false positives
- [ ] not_reviewed findings pass through (nothing silently dropped)
- [ ] Existing tests green
- [ ] `dotnet build` + `dotnet test` clean
