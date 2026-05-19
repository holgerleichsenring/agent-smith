# Pipeline cost cap

Limits the cumulative cost of a single pipeline run. When the cap is reached, remaining LLM-driven skill calls short-circuit with an `Incomplete` outcome (carrying a `cost-cap-exhausted` observation) and the pipeline proceeds to its Compile + Deliver steps so the operator still sees partial output.

## Why it exists

Without a cap, a runaway pipeline — large swagger, adaptive dispatch loop, model misroute — can burn through unbounded budget. p0151d hangs the cap on the existing `PipelineCostTracker.PerSkillBreakdown` (introduced in p0132a/b), so cost accounting stays in one place.

## Defaults

```yaml
pipeline_cost_cap:
  default:
    usd: 5
    tokens: 500000
```

These defaults give ~10-30 LLM calls of headroom for typical pipelines. Operators raise them intentionally for deep audits.

## Per-pipeline overrides

```yaml
pipeline_cost_cap:
  default:
    usd: 5
    tokens: 500000
  per_pipeline:
    api-security-scan:
      usd: 10
      tokens: 1000000
    fix-bug:
      usd: 2
      tokens: 200000
```

Resolution falls back to `default` when the active pipeline name is not in `per_pipeline`. Names match the values registered in `PipelinePresets` (`api-security-scan`, `security-scan`, `fix-bug`, etc.).

## Behavior at the cap

- `PipelineCostTracker.IsBudgetExhausted` flips to `true` once the cumulative USD or token total crosses either threshold.
- `SkillCallRuntime.ExecuteAsync` checks the flag on entry; when set, it skips the LLM call and returns a `SkillCallResult` with:
  - `Outcome = SkillCallOutcome.Incomplete`
  - `FailureReason = "cost cap exhausted"`
  - One `RuntimeObservation` with `Category = "cost-cap-exhausted"` and the actual USD/token totals in `Description`.
- The pipeline executor sees the `Incomplete` outcomes for skipped skills (same path as token/wall-clock caps), and runs Compile + Deliver normally.

## Operator response

If a pipeline reports `cost-cap-exhausted` and the partial output is unusable, the cap is too tight for that workload. Raise the `default` or the per-pipeline override. If the cap is reached repeatedly without unusual workload, the skill catalog is over-fanning out — inspect `PerSkillBreakdown` to see which skill consumed the budget.

If no cap appears in `agentsmith.yml`, the defaults apply.
