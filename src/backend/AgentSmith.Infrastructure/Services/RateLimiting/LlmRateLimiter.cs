using System.Threading.RateLimiting;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: composite limiter over two token buckets — one counts requests
/// (acquire 1 per call), the other counts input tokens (acquire N per call).
/// A call only proceeds when both buckets have capacity. Buckets refill at
/// the per-minute rate the operator configured.
/// </summary>
internal sealed class LlmRateLimiter : ILlmRateLimiter
{
    private readonly TokenBucketRateLimiter _requests;
    private readonly TokenBucketRateLimiter _tokens;

    public LlmRateLimiter(LlmRateLimitOptions options)
    {
        // ReplenishmentPeriod is 1 second so the bucket trickles continuously
        // instead of refilling once per minute (which would push every caller
        // into a synchronized 60s wait). Per-second refill = limit/60.
        var requestsPerSecond = Math.Max(1, options.RequestsPerMinute / 60);
        var tokensPerSecond = Math.Max(1, options.InputTokensPerMinute / 60);
        _requests = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.RequestsPerMinute,
            TokensPerPeriod = requestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
        _tokens = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.InputTokensPerMinute,
            TokensPerPeriod = tokensPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public async Task<IDisposable> AcquireAsync(int estimatedInputTokens, CancellationToken cancellationToken)
    {
        var tokensToConsume = Math.Max(1, estimatedInputTokens);
        // Acquire both leases. Order doesn't matter for correctness; doing
        // requests first means a TPM-starved burst still consumes its RPM slot
        // promptly, which keeps the queue order intuitive for an operator
        // reading the log.
        var reqLease = await _requests.AcquireAsync(1, cancellationToken);
        var tokLease = await _tokens.AcquireAsync(tokensToConsume, cancellationToken);
        return new CompositeLease(reqLease, tokLease);
    }

    private sealed class CompositeLease : IDisposable
    {
        private readonly System.Threading.RateLimiting.RateLimitLease _a;
        private readonly System.Threading.RateLimiting.RateLimitLease _b;
        public CompositeLease(System.Threading.RateLimiting.RateLimitLease a, System.Threading.RateLimiting.RateLimitLease b) { _a = a; _b = b; }
        public void Dispose() { _a.Dispose(); _b.Dispose(); }
    }
}
