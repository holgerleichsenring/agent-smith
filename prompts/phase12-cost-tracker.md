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
