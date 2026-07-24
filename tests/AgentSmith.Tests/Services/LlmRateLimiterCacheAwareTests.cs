using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0374: the rate limiter reserves against the FRESH input share, not the whole
/// re-sent context. Prompt caching (p0374) makes a read-heavy master loop ~99%
/// cache-read, so the old char-estimate throttled a near-free call as if it spent
/// its full ~45k context — one call/minute on the oat token's 20k budget.
/// </summary>
public sealed class LlmRateLimiterCacheAwareTests
{
    // ── the pure discount ───────────────────────────────────────────────────
    [Fact]
    public void EffectiveTokens_AllFresh_ReservesTheFullEstimate()
        => LlmRateLimiter.EffectiveTokens(45_000, cachedFraction: 0.0).Should().Be(45_000);

    [Fact]
    public void EffectiveTokens_AllCached_ReservesOnlyTheCacheWeightTenth()
        => LlmRateLimiter.EffectiveTokens(45_000, cachedFraction: 1.0).Should().Be(4_500);

    [Fact]
    public void EffectiveTokens_HalfCached_ReservesTheBlend()
        // 45000 * (1 - 0.5*0.9) = 45000 * 0.55 = 24750 (±1 for Ceiling/FP)
        => LlmRateLimiter.EffectiveTokens(45_000, cachedFraction: 0.5).Should().BeCloseTo(24_750, 1);

    [Fact]
    public void EffectiveTokens_NeverBelowOne()
        => LlmRateLimiter.EffectiveTokens(1, cachedFraction: 1.0).Should().BeGreaterThanOrEqualTo(1);

    // ── the learned ratio ───────────────────────────────────────────────────
    private static LlmRateLimiter New() => new(new LlmRateLimitOptions(RequestsPerMinute: 60, InputTokensPerMinute: 20_000));

    [Fact]
    public void RecordUsage_FirstCachedCall_SeedsTheFractionHigh()
    {
        var limiter = New();
        limiter.ObservedCachedFraction.Should().Be(0.0, "no observation yet → assume all fresh (conservative)");

        limiter.RecordUsage(freshInputTokens: 2, cachedInputTokens: 45_000);

        limiter.ObservedCachedFraction.Should().BeApproximately(0.99996, 0.001);
    }

    [Fact]
    public void RecordUsage_AfterCachedWarmup_ReservationCollapses()
    {
        var limiter = New();
        // A master-loop steady state: tiny fresh, big cached, a few turns.
        for (var i = 0; i < 5; i++) limiter.RecordUsage(2, 45_000);

        var reserve = LlmRateLimiter.EffectiveTokens(45_000, limiter.ObservedCachedFraction);
        reserve.Should().BeLessThan(6_000,
            "a 99%-cached call should reserve ~a tenth, not the full 45k that caused 55s waits");
    }

    [Fact]
    public void RecordUsage_UncachedBurst_BlendsBackTowardFresh()
    {
        var limiter = New();
        limiter.RecordUsage(2, 45_000);                 // warm cache
        for (var i = 0; i < 5; i++) limiter.RecordUsage(30_000, 0);  // a run of fresh calls

        limiter.ObservedCachedFraction.Should().BeLessThan(0.3,
            "the EWMA blends back so a genuinely fresh workload is throttled again");
    }

    [Fact]
    public void RecordUsage_EmptyUsage_IsIgnored()
    {
        var limiter = New();
        limiter.RecordUsage(2, 45_000);
        var before = limiter.ObservedCachedFraction;

        limiter.RecordUsage(0, 0);

        limiter.ObservedCachedFraction.Should().Be(before, "a zero-token usage report must not move the ratio");
    }
}
