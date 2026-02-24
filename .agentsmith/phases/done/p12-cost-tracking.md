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


---

# Phase 12: Cost Tracker - Implementation Details

## Overview
Calculates monetary cost from token usage by multiplying token counts with
per-model pricing from configuration. Supports per-phase breakdown.

---

## PricingConfig (Contracts Layer)

```csharp
// Configuration/PricingConfig.cs
public class PricingConfig
{
    public Dictionary<string, ModelPricing> Models { get; set; } = new();
}

public class ModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
}
```

Added to `AgentConfig`:
```csharp
public PricingConfig Pricing { get; set; } = new();
```

---

## CostTracker (Infrastructure Layer)

```csharp
// Providers/Agent/CostTracker.cs
public sealed class CostTracker(PricingConfig pricing, ILogger logger)
```

### Key Methods
- `SetPhaseModel(string phase, string model)` - Maps phase to model for pricing lookup
- `CalculateCost(TokenUsageTracker tracker)` - Returns `RunCostSummary` from phase breakdown
- `LogCostSummary(RunCostSummary summary)` - Logs formatted cost breakdown

### Cost Calculation
```
phaseCost = (inputTokens * inputPrice + outputTokens * outputPrice 
             + cacheReadTokens * cacheReadPrice) / 1_000_000
```

If model not found in pricing config → cost = 0, log warning.

---

## RunCostSummary (Infrastructure Layer)

```csharp
public sealed record RunCostSummary(
    IReadOnlyDictionary<string, PhaseCost> Phases,
    decimal TotalCost);

public sealed record PhaseCost(
    string Model, int InputTokens, int OutputTokens,
    int CacheReadTokens, int Iterations, decimal Cost);
```

---

## Config Example

```yaml
pricing:
  models:
    claude-sonnet-4-20250514:
      input_per_million: 3.0
      output_per_million: 15.0
      cache_read_per_million: 0.30
    claude-haiku-4-5-20251001:
      input_per_million: 0.80
      output_per_million: 4.0
      cache_read_per_million: 0.08
```


---

# Phase 12: Phase-Aware Token Tracking - Implementation Details

## Overview
Extend TokenUsageTracker to track which tokens belong to which execution phase.
This enables accurate cost breakdown by phase (scout, planning, primary, compaction).

---

## TokenUsageTracker Extensions

### New Fields
```csharp
private readonly Dictionary<string, PhaseUsage> _phases = new();
private string _currentPhase = "primary";
```

### New Methods
- `SetPhase(string phase)` - Sets the current phase for subsequent Track() calls
- `GetPhaseBreakdown()` - Returns per-phase token usage

### Track() Changes
Each `Track()` call now also accumulates into the current phase:

```csharp
public void Track(MessageResponse response)
{
    // ... existing total tracking ...
    
    if (!_phases.TryGetValue(_currentPhase, out var phaseUsage))
    {
        phaseUsage = new PhaseUsage();
        _phases[_currentPhase] = phaseUsage;
    }
    phaseUsage.InputTokens += usage.InputTokens;
    phaseUsage.OutputTokens += usage.OutputTokens;
    phaseUsage.CacheReadTokens += usage.CacheReadInputTokens;
    phaseUsage.Iterations++;
}
```

---

## PhaseUsage Class

```csharp
public sealed class PhaseUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int Iterations { get; set; }
}
```

---

## Integration in ClaudeAgentProvider

Phase transitions:
1. Scout: `tracker.SetPhase("scout")` before ScoutAgent runs
2. Primary: `tracker.SetPhase("primary")` before AgenticLoop runs
3. Compaction: ClaudeContextCompactor temporarily switches to "compaction" phase

The CostTracker maps each phase name to its model for pricing calculation.
