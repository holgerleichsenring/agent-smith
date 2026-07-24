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

    // p0374: a cache-read token still costs the provider something, but far less
    // than a fresh one — reserve cached tokens at this fraction of their size so a
    // 99%-cached call doesn't drain the per-minute budget like a fully-fresh one.
    // Leaves headroom: if the provider does throttle harder than this assumes, the
    // 429/Retry-After path (SDK + TransientRetryChatClient) self-corrects.
    private const double CacheWeight = 0.1;
    private const double RatioAlpha = 0.4;   // EWMA responsiveness — adapts in ~2-3 calls

    private readonly object _ratioLock = new();
    private double _cachedFraction;          // EWMA of cached / (fresh + cached); 0 = all fresh
    private bool _hasSample;

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
        // p0374: scale the char-based estimate down by the observed cache share so
        // the reservation reflects the FRESH load, not the whole re-sent context.
        double cachedFraction;
        lock (_ratioLock) cachedFraction = _cachedFraction;
        var tokensToConsume = Math.Clamp(EffectiveTokens(estimatedInputTokens, cachedFraction), 1, _tokenBucketCapacity);
        // Acquire both leases. Order doesn't matter for correctness; doing
        // requests first means a TPM-starved burst still consumes its RPM slot
        // promptly, which keeps the queue order intuitive for an operator
        // reading the log.
        var reqLease = await _requests.AcquireAsync(1, cancellationToken);
        var tokLease = await _tokens.AcquireAsync(tokensToConsume, cancellationToken);
        return new CompositeLease(reqLease, tokLease);
    }

    // The reservation size for a call, discounted by the observed cache share. Runs
    // from the full estimate (all fresh) down to CacheWeight × estimate (all cached).
    internal static int EffectiveTokens(int estimatedInputTokens, double cachedFraction)
    {
        var discount = 1.0 - Math.Clamp(cachedFraction, 0.0, 1.0) * (1.0 - CacheWeight);
        return Math.Max(1, (int)Math.Ceiling(estimatedInputTokens * discount));
    }

    // The current EWMA cache share — for tests + diagnostics.
    internal double ObservedCachedFraction { get { lock (_ratioLock) return _cachedFraction; } }

    public void RecordUsage(long freshInputTokens, long cachedInputTokens)
    {
        var total = freshInputTokens + cachedInputTokens;
        if (total <= 0) return;
        var fraction = (double)cachedInputTokens / total;
        lock (_ratioLock)
        {
            // Seed on the first observation, then blend so a burst of uncached calls
            // (e.g. scope/analyze) doesn't instantly undo a warmed cache ratio.
            _cachedFraction = _hasSample ? (RatioAlpha * fraction) + ((1 - RatioAlpha) * _cachedFraction) : fraction;
            _hasSample = true;
        }
    }

    private sealed class CompositeLease : IDisposable
    {
        private readonly System.Threading.RateLimiting.RateLimitLease _a;
        private readonly System.Threading.RateLimiting.RateLimitLease _b;
        public CompositeLease(System.Threading.RateLimiting.RateLimitLease a, System.Threading.RateLimiting.RateLimitLease b) { _a = a; _b = b; }
        public void Dispose() { _a.Dispose(); _b.Dispose(); }
    }
}
