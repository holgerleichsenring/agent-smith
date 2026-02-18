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
