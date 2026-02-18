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
