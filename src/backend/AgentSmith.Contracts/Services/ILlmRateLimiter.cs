namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0188: process-wide gate on LLM provider calls. Two independent budgets are
/// tracked — requests-per-minute and input-tokens-per-minute — and the caller
/// blocks until both have capacity for the impending call. The same
/// <see cref="ILlmRateLimiter"/> instance is shared across every code path that
/// hits the same provider/model, so concurrent sub-agents, the master, and
/// every analyzer/scout call queue against the same budget.
///
/// Rationale: even on a generous API-key tier, the master loop + fan-out
/// sub-agents easily produce 60+ requests in the first minute and Anthropic /
/// OpenAI both rate-limit per-minute. A reactive Retry-After handler papers
/// over the first few 429s; a process-wide proactive limit keeps the bursting
/// behavior from ever asking the provider for more than it will give.
/// </summary>
public interface ILlmRateLimiter
{
    /// <summary>
    /// Blocks until both budgets (requests + input tokens) have capacity for
    /// one request of the given estimated input-token size, then returns a
    /// lease that the caller disposes when the call completes. Disposal does
    /// not return the consumed budget — token buckets refill over time, not
    /// on release.
    /// </summary>
    Task<IDisposable> AcquireAsync(int estimatedInputTokens, CancellationToken cancellationToken);
}

/// <summary>
/// p0188: factory keyed by provider+model, holding one shared limiter per
/// (provider, model) pair. ChatClientFactory consults the registry when it
/// wraps a bare IChatClient so every call against the same provider/model
/// goes through the same budget.
/// </summary>
public interface ILlmRateLimiterRegistry
{
    ILlmRateLimiter GetOrCreate(string providerType, string model, LlmRateLimitOptions options);
}

/// <summary>
/// p0188: per-limiter configuration. Defaults are conservative-ish for the
/// subscription tier; operator overrides via AgentConfig.RateLimit win.
/// </summary>
public sealed record LlmRateLimitOptions(int RequestsPerMinute, int InputTokensPerMinute);
