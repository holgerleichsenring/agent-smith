namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0188: process-wide gate on LLM provider calls to one model deployment. Two budgets are tracked — requests and
/// tokens per minute — and every caller of the deployment (master, sub-agents, compaction, any other task) blocks
/// until both have room for its call.
/// <para>
/// 2026-11-22-66e4a: the budget follows what the PROVIDER counts. A call reserves an estimate on entry and settles
/// to the provider's own count afterwards (<see cref="ILlmQuotaLease.Settle"/>); the provider's remaining-quota
/// headers cap the budget; a throughput refusal pauses the whole deployment (<see cref="ILlmQuotaLease.Refused"/>).
/// </para>
/// </summary>
public interface ILlmRateLimiter
{
    /// <summary>Blocks until the deployment has room for <paramref name="demand"/>, then returns the call's lease.
    /// Room refills over time; a lease returns nothing on its own.</summary>
    Task<ILlmQuotaLease> AcquireAsync(LlmQuotaDemand demand, CancellationToken cancellationToken);
}

/// <summary>
/// p0188: one shared limiter per model deployment (<see cref="ModelQuotaScope"/>). ChatClientFactory consults the
/// registry when it wraps a bare IChatClient, so every call to the same deployment draws on the same budget.
/// </summary>
public interface ILlmRateLimiterRegistry
{
    ILlmRateLimiter GetOrCreate(ModelQuotaScope scope, LlmRateLimitOptions options);
}

/// <summary>
/// p0188: per-limiter configuration. Defaults are the provider tier; operator overrides via AgentConfig.RateLimit
/// win. 2026-11-22-66e4a: <paramref name="Rule"/> says how the provider counts a call; null counts every token.
/// </summary>
public sealed record LlmRateLimitOptions(int RequestsPerMinute, int InputTokensPerMinute, ProviderQuotaRule? Rule = null);
