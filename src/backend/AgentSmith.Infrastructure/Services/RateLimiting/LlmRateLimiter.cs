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
    private readonly int _tokenBucketCapacity;

    public LlmRateLimiter(LlmRateLimitOptions options)
    {
        // p0350: the token bucket's total capacity. A single acquire larger than
        // this can NEVER be satisfied, and .NET's TokenBucketRateLimiter answers
        // that with a hard ArgumentOutOfRangeException ("{n} token(s) exceeds the
        // token limit of {limit}"), NOT a wait — so the per-minute THROTTLE
        // silently doubled as an un-queueable per-call CEILING and crashed a run
        // that had done all its work (opened its PRs, 21/22 steps). We clamp the
        // acquire to this capacity below so an over-budget single call waits for a
        // full bucket and proceeds, instead of killing the run.
        _tokenBucketCapacity = options.InputTokensPerMinute;
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
        // p0350: clamp to the bucket capacity — a single call larger than the
        // whole per-minute budget still WAITS for a full bucket and proceeds
        // (throttled), rather than throwing and losing the run. The per-minute
        // accounting under-counts such a giant call, which is the right trade:
        // the bucket's job is to pace frequency, not to be a hard size wall.
        var tokensToConsume = Math.Clamp(estimatedInputTokens, 1, _tokenBucketCapacity);
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
