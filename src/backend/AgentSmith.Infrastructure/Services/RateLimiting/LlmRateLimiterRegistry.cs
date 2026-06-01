using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: keyed cache of <see cref="ILlmRateLimiter"/> instances. One limiter
/// per (provider, model) pair, lazily created on first use, lives for the
/// process lifetime. Two call sites referencing the same provider+model share
/// a single budget — which is the whole point of the abstraction.
/// </summary>
internal sealed class LlmRateLimiterRegistry : ILlmRateLimiterRegistry
{
    private readonly ConcurrentDictionary<string, ILlmRateLimiter> _limiters = new(StringComparer.Ordinal);
    private readonly ILogger<LlmRateLimiterRegistry> _logger;

    public LlmRateLimiterRegistry(ILogger<LlmRateLimiterRegistry> logger)
    {
        _logger = logger;
    }

    public ILlmRateLimiter GetOrCreate(string providerType, string model, LlmRateLimitOptions options)
    {
        var key = BuildKey(providerType, model);
        return _limiters.GetOrAdd(key, _ =>
        {
            _logger.LogInformation(
                "Created LLM rate limiter for {Key}: {Rpm} req/min, {Tpm} tokens/min",
                key, options.RequestsPerMinute, options.InputTokensPerMinute);
            return new LlmRateLimiter(options);
        });
    }

    private static string BuildKey(string providerType, string model) =>
        $"{providerType.ToLowerInvariant()}::{model.ToLowerInvariant()}";
}
