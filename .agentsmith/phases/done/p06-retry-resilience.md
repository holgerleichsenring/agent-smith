# Phase 6: Resilience - Retry with Exponential Backoff - Implementation Plan

## Goal
Agent Smith survives Rate Limits (429) and transient errors (500/502/503).
Prerequisite for all further optimizations (Caching, Compaction, Scout).

---

## Prerequisite
- Phase 5 completed (CLI functional)
- First E2E test documented (run-log-001): Crash at Iteration 6 due to Rate Limit

## Findings from SDK Analysis

Anthropic.SDK 5.9.0 provides:
- **No** built-in RetryInterceptor
- `AnthropicClient(string apiKey, HttpClient httpClient)` → HttpClient injection possible
- `RateLimitsExceeded` Exception (catchable)
- `RateLimits` Type on Response (RequestsLimit, etc.)
- `Usage` with InputTokens, OutputTokens, CacheCreationInputTokens, CacheReadInputTokens

Strategy: **Polly via `Microsoft.Extensions.Http.Resilience`** as DelegatingHandler on the HttpClient.

## Steps

### Step 1: RetryConfig + Resilient HttpClient Factory
See: `prompts/phase6-retry.md`

New config class for retry settings and a factory that creates HttpClients with
Polly-based retry handler.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Integration in ClaudeAgentProvider + AgenticLoop
See: `prompts/phase6-retry.md` (second part)

Adapt existing classes: Use resilient client, log token usage.
Project: `AgentSmith.Infrastructure/`

### Step 3: Config + Tests + Verify
Extend config, write tests, validate E2E.

---

## Dependencies

```
Step 1 (RetryConfig + Factory)
    └── Step 2 (Integration Provider + Loop)
         └── Step 3 (Config + Tests + Verify)
```

Strictly sequential.

---

## NuGet Packages (Phase 6)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Infrastructure | Microsoft.Extensions.Http.Resilience | Polly-based HttpClient Retry |
| AgentSmith.Infrastructure | Microsoft.Extensions.Http | HttpClientFactory |

---

## Definition of Done (Phase 6)
- [ ] `RetryConfig` class in Contracts with sensible defaults
- [ ] `AgentConfig.Retry` Property
- [ ] Resilient HttpClient with Polly: Retry on 429, 500, 502, 503, 504
- [ ] Exponential Backoff with Jitter
- [ ] `ClaudeAgentProvider` uses resilient client for Plan + Execution
- [ ] `AgenticLoop` logs token usage per iteration
- [ ] YAML Config supports `retry:` section (backward-compatible)
- [ ] All existing tests green
- [ ] New unit tests for Retry Config
- [ ] E2E test survives Rate Limits


---

# Phase 6 - Step 1 & 2: Retry with Exponential Backoff

## Goal
All Anthropic API calls survive transient errors and Rate Limits automatically.
Project: `AgentSmith.Contracts/Configuration/`, `AgentSmith.Infrastructure/Providers/Agent/`

---

## RetryConfig

```
File: src/AgentSmith.Contracts/Configuration/RetryConfig.cs
```

Simple config class with sensible defaults:

- `int MaxRetries` = 5
- `int InitialDelayMs` = 2000 (2 seconds)
- `double BackoffMultiplier` = 2.0
- `int MaxDelayMs` = 60000 (1 minute)

### Notes
- No `UseJitter` property needed - Polly has built-in Jitter
- Defaults are conservative enough for Tier 1 (30k ITPM)

---

## AgentConfig Extension

```
File: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```

New property:
- `RetryConfig Retry { get; set; } = new();`

Backward-compatible: Default constructor provides sensible values.

---

## ResilientHttpClientFactory

```
File: src/AgentSmith.Infrastructure/Providers/Agent/ResilientHttpClientFactory.cs
```

**Responsibility:** Creates HttpClient instances with Polly Retry Policy.

### Behavior
1. Creates `HttpClient` with `SocketsHttpHandler` as base
2. Adds Polly `RetryPolicy` as `DelegatingHandler`
3. Retry on HTTP Status: 429, 500, 502, 503, 504
4. Exponential Backoff: `initialDelay * Math.Pow(backoffMultiplier, retryAttempt)`
5. Jitter: +/-25% on each delay (prevents Thundering Herd)
6. Logs each retry attempt with wait time

### Code Sketch

```csharp
public sealed class ResilientHttpClientFactory(
    RetryConfig config,
    ILogger<ResilientHttpClientFactory> logger)
{
    public HttpClient Create()
    {
        var retryPolicy = CreateRetryPolicy();
        var handler = new PolicyHttpMessageHandler(retryPolicy)
        {
            InnerHandler = new SocketsHttpHandler()
        };
        return new HttpClient(handler);
    }

    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => IsTransientOrRateLimit(r.StatusCode))
            .WaitAndRetryAsync(
                config.MaxRetries,
                retryAttempt => CalculateDelay(retryAttempt),
                onRetry: (outcome, delay, retryCount, _) =>
                    logger.LogWarning(
                        "Retry {Count}/{Max} after {Delay}ms (HTTP {Status})",
                        retryCount, config.MaxRetries, delay.TotalMilliseconds,
                        outcome.Result?.StatusCode));
    }
}
```

### Notes
- `PolicyHttpMessageHandler` comes from `Microsoft.Extensions.Http.Polly`
- Alternative: Use `Polly` directly without Microsoft.Extensions.Http
- Check whether `Microsoft.Extensions.Http.Resilience` or `Polly` is a better fit
- The HttpClient is passed to `new AnthropicClient(apiKey, httpClient)`

---

## ClaudeAgentProvider Adjustment

```
File: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

### Changes

**Constructor:** Extend with `RetryConfig retryConfig`

```csharp
public sealed class ClaudeAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    ILogger<ClaudeAgentProvider> logger) : IAgentProvider
```

**Client creation:** Instead of `new AnthropicClient(apiKey)`:

```csharp
private AnthropicClient CreateClient()
{
    var factory = new ResilientHttpClientFactory(retryConfig, ...);
    var httpClient = factory.Create();
    return new AnthropicClient(apiKey, httpClient);
}
```

**Both methods** (`GeneratePlanAsync`, `ExecutePlanAsync`) use `CreateClient()`.

---

## AgenticLoop Adjustment

```
File: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

### Changes

After each API call: Log token usage.

```csharp
private void LogUsage(MessageResponse response, int iteration)
{
    var usage = response.Usage;
    logger.LogDebug(
        "Iteration {Iter}: Input={Input}, Output={Output}, CacheCreate={CacheCreate}, CacheRead={CacheRead}",
        iteration,
        usage.InputTokens,
        usage.OutputTokens,
        usage.CacheCreationInputTokens ?? 0,
        usage.CacheReadInputTokens ?? 0);
}
```

### Notes
- No TokenUsageTracker yet (comes in Phase 7)
- For now only logging per iteration for observability
- `Usage.CacheCreationInputTokens` and `CacheReadInputTokens` are `int?`

---

## AgentProviderFactory Adjustment

```
File: src/AgentSmith.Infrastructure/Factories/AgentProviderFactory.cs
```

`CreateClaude` passes `config.Retry` through:

```csharp
private ClaudeAgentProvider CreateClaude(AgentConfig config)
{
    var apiKey = secrets.GetRequired("ANTHROPIC_API_KEY");
    return new ClaudeAgentProvider(
        apiKey, config.Model, config.Retry,
        loggerFactory.CreateLogger<ClaudeAgentProvider>());
}
```

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
```

---

## Tests

**RetryConfigTests:**
- `Defaults_AreReasonable` - Check default values
- `YamlDeserialization_Works` - Load config from YAML

**ResilientHttpClientFactoryTests:**
- `Create_ReturnsHttpClient` - Not null
- `RetryPolicy_Retries429` - Mock HTTP, verify that 429 is retried

**AgentProviderFactoryTests:**
- Adapt existing tests for new constructor parameter

**DiRegistrationTests:**
- All existing tests must remain green
