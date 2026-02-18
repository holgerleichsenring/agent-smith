# Phase 12: Cost Tracking - Implementation Plan

## Goal
After every run, document the cost: total amount, currency, broken down by phase.
Prices come from configuration (not hardcoded), so users can update them when providers change pricing.

---

## Prerequisite
- Phase 10 completed (Container Production-Ready)

## Steps

### Step 1: PricingConfig + CostTracker
See: `prompts/phase12-cost-tracker.md`

Configuration for per-model pricing, cost calculation from token usage.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Phase-Aware Token Tracking
See: `prompts/phase12-phase-tracking.md`

Extend TokenUsageTracker with phase breakdown (scout, planning, primary, compaction).
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (PricingConfig + CostTracker)
    └── Step 2 (Phase-Aware Tracking + Provider Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 12)

No new packages required.

---

## Key Decisions

1. Prices in config, not hardcoded (providers change pricing frequently)
2. Cost = (InputTokens * InputPrice + OutputTokens * OutputPrice + CacheReadTokens * CacheReadPrice) / 1,000,000
3. Phase tracking: each API call belongs to a phase (scout, planning, primary, compaction)
4. CostTracker maps phase → model → pricing for accurate breakdown
5. Missing model pricing defaults to zero (no error, just warns)

---

## Output Example
```
Run completed: $3.42 total
  Scout (haiku):       $0.05 (63k input, 2k output)
  Planning (sonnet):   $0.12 (5k input, 1k output)
  Primary (sonnet):    $3.10 (93k input, 4k output)
  Compaction (haiku):  $0.15 (12k input, 1k output)
```

---

## Definition of Done (Phase 12)
- [ ] `PricingConfig` + `ModelPricing` in Contracts
- [ ] `AgentConfig.Pricing` property
- [ ] `TokenUsageTracker.SetPhase()` + `GetPhaseBreakdown()`
- [ ] `PhaseUsage` class (InputTokens, OutputTokens, CacheReadTokens, Iterations)
- [ ] `CostTracker` in Infrastructure (calculates cost from tracker × pricing)
- [ ] `RunCostSummary` + `PhaseCost` records
- [ ] ClaudeAgentProvider integrates CostTracker, logs cost summary
- [ ] ScoutAgent and ClaudeContextCompactor report usage to shared tracker
- [ ] Pricing section in agentsmith.yml
- [ ] Unit tests for CostTracker
- [ ] All existing tests green
