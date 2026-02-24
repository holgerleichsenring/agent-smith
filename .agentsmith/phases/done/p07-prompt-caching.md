# Phase 7: Prompt Caching - Implementation Plan

## Goal
Activate Anthropic Prompt Caching so that System Prompt, Tool Definitions, and Coding
Principles are cached server-side. Cached tokens do NOT count against the ITPM
Rate Limit - the single biggest lever for our Rate Limit problems.

---

## Prerequisite
- Phase 6 completed (Retry logic in place as safety net for cache misses)

## SDK Findings

Anthropic.SDK 5.9.0 provides:
- `MessageParameters.PromptCaching` Property (Type: `PromptCacheType`)
  - `None` = 0 (no caching)
  - `FineGrained` = 1 (manual via `CacheControl` on SystemMessages/Content)
  - `AutomaticToolsAndSystem` = 2 (automatic for all System Messages and Tools)
- `SystemMessage(text, cacheControl)` Constructor
- `CacheControl { Type = CacheControlType.ephemeral, TTL = null }` (5 min default)
- `Usage.CacheCreationInputTokens` / `Usage.CacheReadInputTokens` on Response

## Steps

### Step 1: CacheConfig + TokenUsageTracker
See: `prompts/phase7-caching.md`

New config class and token tracking for observability.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Activate Prompt Caching
See: `prompts/phase7-token-tracking.md`

Set `PromptCaching = PromptCacheType.AutomaticToolsAndSystem` on all API calls.
Restructure System Prompt for optimal cache prefix.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (CacheConfig + Tracker)
    └── Step 2 (Activate Caching)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 7)

No new packages needed. Everything is included in Anthropic.SDK 5.9.0.

---

## Definition of Done (Phase 7)
- [ ] `CacheConfig` class in Contracts
- [ ] `AgentConfig.Cache` Property
- [ ] `PromptCaching = AutomaticToolsAndSystem` on all API calls (when enabled)
- [ ] System Prompt optimally structured (Coding Principles first = longest stable prefix)
- [ ] `TokenUsageTracker` logs cumulative usage incl. cache metrics
- [ ] Cache Hit Rate visible in logs
- [ ] All existing tests green
- [ ] New unit tests for TokenUsageTracker
- [ ] E2E shows cache hits from Iteration 2 onwards


---

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


---

# Phase 7 - Step 2: Activate Prompt Caching

## Goal
Set `PromptCaching = AutomaticToolsAndSystem` on all API calls and restructure the
System Prompt for optimal caching.
Project: `AgentSmith.Infrastructure/Providers/Agent/`

---

## AgenticLoop Adjustment

```
File: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

### Changes

1. Constructor: `CacheConfig cacheConfig` and `TokenUsageTracker tracker` as parameters
2. In `SendRequestAsync`, set `PromptCaching`:
   ```csharp
   PromptCaching = ResolveCacheType(cacheConfig)
   ```
3. After each API call: `tracker.Track(response)`
4. At the end of `RunAsync`: `tracker.LogSummary(logger)`
5. Existing `LogTokenUsage` method remains for detail logging per iteration

### ResolveCacheType Helper
```csharp
private static PromptCacheType ResolveCacheType(CacheConfig config)
{
    if (!config.Enabled) return PromptCacheType.None;
    return config.Strategy.ToLowerInvariant() switch
    {
        "automatic" => PromptCacheType.AutomaticToolsAndSystem,
        "fine-grained" => PromptCacheType.FineGrained,
        _ => PromptCacheType.None
    };
}
```

---

## ClaudeAgentProvider Adjustment

```
File: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

### Changes

1. System Prompt restructuring for optimal cache prefix:
   - **First SystemMessage**: Coding Principles (large, static per execution)
   - **Second SystemMessage**: Execution Instructions (small, varies)
   - Reason: Cache is prefix-based. The longest stable prefix gets cached.

2. In `GeneratePlanAsync`: Set `PromptCaching`
   ```csharp
   PromptCaching = ResolveCacheType(cacheConfig)
   ```

3. In `ExecutePlanAsync`: Create `TokenUsageTracker`, pass to `AgenticLoop`

4. Constructor: Obtain `CacheConfig` from `AgentConfig` (via `retryConfig` pattern)

### System Prompt Structure (optimized for caching)
```
SystemMessage[0]: Coding Principles      ← ~1.5k tokens, CACHED after 1st call
SystemMessage[1]: Task Instructions       ← ~400 tokens, CACHED after 1st call
Tool Definitions (4 Tools)               ← ~800 tokens, CACHED automatically
```

From Iteration 2 onwards: ~2.7k tokens are read from cache instead of being counted.
At 30k ITPM limit (Tier 1) this saves ~9% per iteration.

---

## AgentProviderFactory Adjustment

`CreateClaude` passes `config.Cache` through (same as `config.Retry`).

---

## Config Example

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  retry:
    max_retries: 5
    initial_delay_ms: 2000
    backoff_multiplier: 2.0
    max_delay_ms: 60000
  cache:
    enabled: true
    strategy: automatic
```
