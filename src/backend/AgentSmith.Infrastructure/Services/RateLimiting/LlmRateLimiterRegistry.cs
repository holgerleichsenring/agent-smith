using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: keyed cache of <see cref="ILlmRateLimiter"/> instances, lazily created on first use and kept for the
/// process lifetime. 2026-11-22-66e4a: keyed by deployment (<see cref="ModelQuotaScope"/>), so every call to one
/// deployment shares one budget whatever model name its role carries; the read-out lists each one's quota.
/// </summary>
internal sealed class LlmRateLimiterRegistry(ILogger<LlmRateLimiterRegistry> logger)
    : ILlmRateLimiterRegistry, ILlmQuotaReader
{
    private readonly ConcurrentDictionary<string, Entry> _limiters = new(StringComparer.Ordinal);

    public ILlmRateLimiter GetOrCreate(ModelQuotaScope scope, LlmRateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var entry = _limiters.GetOrAdd(scope.Key, _ =>
        {
            logger.LogInformation(
                "Created LLM rate limiter for {Key}: {Rpm} req/min, {Tpm} tokens/min",
                scope.Key, options.RequestsPerMinute, options.InputTokensPerMinute);
            return new Entry(scope, new LlmRateLimiter(options)) { Options = options };
        });
        Adopt(entry, options);
        return entry.Limiter;
    }

    // 2026-11-22-66e4b: a budget changed in the configuration reaches the deployment when its next client is built.
    private void Adopt(Entry entry, LlmRateLimitOptions options)
    {
        lock (entry)
        {
            if (entry.Options == options) return;
            entry.Limiter.Adopt(options);
            entry.Options = options;
        }
        logger.LogInformation("LLM rate limiter for {Key} now {Rpm} req/min, {Tpm} tokens/min",
            entry.Scope.Key, options.RequestsPerMinute, options.InputTokensPerMinute);
    }

    public IReadOnlyList<LlmQuotaSnapshot> Read() =>
        _limiters.Values
            .Select(e => e.Limiter.Snapshot(e.Scope))
            .OrderBy(s => s.Deployment, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed record Entry(ModelQuotaScope Scope, LlmRateLimiter Limiter)
    {
        public required LlmRateLimitOptions Options { get; set; }
    }
}
