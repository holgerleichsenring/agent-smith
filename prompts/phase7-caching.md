# Phase 7 - Step 1: CacheConfig + TokenUsageTracker

## Goal
Config class for cache control and a tracker that accumulates token usage across all
iterations and calculates cache hit rates.
Project: `AgentSmith.Contracts/Configuration/`, `AgentSmith.Infrastructure/Providers/Agent/`

---

## CacheConfig

```
File: src/AgentSmith.Contracts/Configuration/CacheConfig.cs
```

- `bool Enabled` = true
- `string Strategy` = "automatic" (Values: "automatic", "fine-grained", "none")

### Notes
- "automatic" → `PromptCacheType.AutomaticToolsAndSystem`
- "fine-grained" → `PromptCacheType.FineGrained` (for later manual control)
- "none" → `PromptCacheType.None`

---

## AgentConfig Extension

```
File: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```

New property:
- `CacheConfig Cache { get; set; } = new();`

---

## TokenUsageSummary

```
File: src/AgentSmith.Infrastructure/Providers/Agent/TokenUsageSummary.cs
```

Sealed record:
```csharp
public sealed record TokenUsageSummary(
    int TotalInputTokens,
    int TotalOutputTokens,
    int CacheCreationTokens,
    int CacheReadTokens,
    int Iterations)
{
    public double CacheHitRate => (TotalInputTokens + CacheReadTokens) > 0
        ? (double)CacheReadTokens / (TotalInputTokens + CacheReadTokens)
        : 0.0;
}
```

---

## TokenUsageTracker

```
File: src/AgentSmith.Infrastructure/Providers/Agent/TokenUsageTracker.cs
```

**Responsibility:** Accumulates token usage across all API calls of an execution.

### Behavior
1. `Track(MessageResponse response)` → extracts Usage, adds to total
2. `GetSummary()` → returns `TokenUsageSummary`
3. `LogSummary(ILogger logger)` → logs summary at the end

### Code Sketch
```csharp
public sealed class TokenUsageTracker
{
    private int _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations;

    public void Track(MessageResponse response)
    {
        var usage = response.Usage;
        _totalInput += usage.InputTokens;
        _totalOutput += usage.OutputTokens;
        _cacheCreate += usage.CacheCreationInputTokens;
        _cacheRead += usage.CacheReadInputTokens;
        _iterations++;
    }

    public TokenUsageSummary GetSummary() => new(
        _totalInput, _totalOutput, _cacheCreate, _cacheRead, _iterations);

    public void LogSummary(ILogger logger)
    {
        var summary = GetSummary();
        logger.LogInformation(
            "Token usage: {Input} input, {Output} output, " +
            "{CacheCreate} cache-create, {CacheRead} cache-read, " +
            "Cache hit rate: {Rate:P1}, Iterations: {Iter}",
            summary.TotalInputTokens, summary.TotalOutputTokens,
            summary.CacheCreationTokens, summary.CacheReadTokens,
            summary.CacheHitRate, summary.Iterations);
    }
}
```

## Tests

**TokenUsageTrackerTests:**
- `Track_AccumulatesTokens`
- `GetSummary_ReturnsCorrectTotals`
- `CacheHitRate_CalculatesCorrectly`
- `CacheHitRate_ZeroTokens_ReturnsZero`
