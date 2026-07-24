using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0376: the per-pipeline token cap must weight cache-read tokens the way the
/// USD cap already prices them (~1/10th). Heavy history-caching (p0374) makes a
/// master loop re-read a large cached context every call; counting those reads
/// at full weight tripped the token cap at a fraction of the USD cap and killed
/// finished runs one step before their verdict (live: run c6f2, 14.6M cache
/// reads → 15M token cap at only $6.59 of $45). The USD arm must still fire.
/// </summary>
public sealed class PipelineCostTrackerBudgetCacheAwareTests
{
    [Fact]
    public void IsBudgetExhausted_HeavyCacheReadUnderUsdCap_DoesNotTripTokenCap()
    {
        // 15M cache reads: full weight would blow a 5M token cap; at 0.1 weight
        // it is 1.5M — well under. USD stays tiny (cache read is $0.30/M).
        var tracker = new PipelineCostTracker(
            config: SonnetPricing(),
            costCap: new CostCapValues { Usd = 25.0m, Tokens = 5_000_000 });

        for (var i = 0; i < 15; i++)
            tracker.Track(CachedResponse("claude-sonnet-5", cacheRead: 1_000_000, output: 2_000));

        tracker.TotalCacheReadTokens.Should().Be(15_000_000, "raw reporting stays honest");
        tracker.IsBudgetExhausted.Should().BeFalse(
            "15M cache reads weighted at 0.1 = 1.5M, under the 5M token cap, and USD is far under $25");
    }

    [Fact]
    public void IsBudgetExhausted_FreshInputBeyondTokenCap_StillTrips()
    {
        // Fresh (billable) input is counted at full weight — the token cap must
        // still catch a genuine runaway (e.g. an unpriced model where USD is $0).
        var tracker = new PipelineCostTracker(
            costCap: new CostCapValues { Usd = 1000m, Tokens = 5_000_000 });

        tracker.Track(CachedResponse("unpriced-model", cacheRead: 0, input: 6_000_000, output: 0));

        tracker.IsBudgetExhausted.Should().BeTrue(
            "6M fresh input exceeds the 5M token cap regardless of cache weighting");
    }

    [Fact]
    public void IsBudgetExhausted_UsdCapCrossed_TripsEvenWhenTokensLow()
    {
        // The USD arm is independent: a small token volume at a high price still
        // exhausts the budget.
        var tracker = new PipelineCostTracker(
            config: SonnetPricing(),
            costCap: new CostCapValues { Usd = 1.0m, Tokens = 50_000_000 });

        // 1M output at $15/M = $15, over the $1 USD cap; tokens far under.
        tracker.Track(CachedResponse("claude-sonnet-5", output: 1_000_000));

        tracker.IsBudgetExhausted.Should().BeTrue("$15 output cost crosses the $1 USD cap");
    }

    [Fact]
    public void EffectiveBudgetTokens_DiscountsCacheReads_MatchingTheCapArm()
    {
        // p0376: the master loop's own budget fence reads EffectiveBudgetTokens (not raw
        // TotalTokens), so it agrees with IsBudgetExhausted. 10M cache reads must weigh as
        // 1M, not 10M — else the fence trips on cache volume mid-pass (the live 63d0 death:
        // 15.16M full-weight tokens tripped a 15M cap at only ~$15 of a $45 USD cap).
        var tracker = new PipelineCostTracker(config: SonnetPricing());

        tracker.Track(CachedResponse("claude-sonnet-5", cacheRead: 10_000_000, input: 1_000, output: 2_000));

        tracker.TotalTokens.Should().Be(10_003_000, "raw total stays honest for reporting");
        tracker.EffectiveBudgetTokens.Should().Be(1_003_000,
            "cache reads weighted at 0.1: 10M -> 1M, plus 1k fresh + 2k output");
    }

    private static PricingConfig SonnetPricing() => new()
    {
        Models = new()
        {
            ["claude-sonnet-5"] = new()
            {
                InputPerMillion = 3.0m,
                OutputPerMillion = 15.0m,
                CacheReadPerMillion = 0.30m,
            },
        },
    };

    private static ChatResponse CachedResponse(
        string modelId, long cacheRead = 0, long input = 0, long output = 0) => new()
    {
        ModelId = modelId,
        Usage = new UsageDetails
        {
            InputTokenCount = input,
            OutputTokenCount = output,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                ["CacheReadInputTokens"] = cacheRead,
            },
        },
    };
}
