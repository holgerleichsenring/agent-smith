using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0374, 2026-11-22-66e4a: the limiter reserves what the provider will COUNT. Prompt caching makes a read-heavy
/// Anthropic loop ~99% cache reads, which its input limit does not count, so a near-free call must not reserve its
/// whole re-sent context. Azure and OpenAI DO count cached prompt tokens, so there a warm cache discounts nothing.
/// One learned ratio of counted to estimated input carries both.
/// </summary>
public sealed class LlmRateLimiterCacheAwareTests
{
    private static readonly ProviderQuotaRule Anthropic = LlmRateBudget.RuleFor("claude");
    private static readonly ProviderQuotaRule Azure = LlmRateBudget.RuleFor("azure_openai");

    [Fact]
    public void CountedInput_AnthropicWarmCache_CountsATenthOfTheReads()
    {
        var usage = AnthropicUsage(uncached: 2, cacheReads: 45_000, cacheWrites: 100);

        LlmQuotaLease.CountedInput(usage, Anthropic).Should().BeApproximately(2 + 100 + 4_500, 0.01,
            "Anthropic counts uncached input and cache writes, and the tenth of the reads is headroom");
    }

    [Fact]
    public void CountedInput_AzureWarmCache_CountsEveryPromptToken()
    {
        var usage = new UsageDetails { InputTokenCount = 50_000, CachedInputTokenCount = 45_000, OutputTokenCount = 200 };

        LlmQuotaLease.CountedInput(usage, Azure).Should().Be(50_000,
            "Azure's TPM counts the cached prompt tokens the usage reports inside the input count");
    }

    [Fact]
    public async Task Reservation_AfterAWarmAnthropicCall_CollapsesToTheCountedShare()
    {
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(60, 200_000, Anthropic));
        var registry = Snapshotting(limiter);
        var lease = await limiter.AcquireAsync(new LlmQuotaDemand(45_000, 0), CancellationToken.None);
        lease.Settle(AnthropicUsage(uncached: 2, cacheReads: 45_000, cacheWrites: 0), null, TimeSpan.Zero);
        var afterFirst = registry().FreeTokens;

        await limiter.AcquireAsync(new LlmQuotaDemand(45_000, 0), CancellationToken.None);

        (afterFirst - registry().FreeTokens).Should().BeLessThan(6_000,
            "a 99%-cached call reserves about a tenth, not the full 45k that once caused 55 s waits");
    }

    [Fact]
    public async Task Reservation_AfterAnUnderestimatedAzureCall_GrowsToTheCountedSize()
    {
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(250, 250_000, Azure));
        var registry = Snapshotting(limiter);
        var lease = await limiter.AcquireAsync(new LlmQuotaDemand(18_000, 0), CancellationToken.None);
        lease.Settle(new UsageDetails { InputTokenCount = 50_000, CachedInputTokenCount = 40_000 }, null, TimeSpan.Zero);
        var afterFirst = registry().FreeTokens;

        await limiter.AcquireAsync(new LlmQuotaDemand(18_000, 0), CancellationToken.None);

        (afterFirst - registry().FreeTokens).Should().BeGreaterThan(45_000,
            "chars/4 read 18k where Azure counted 50k; the next call reserves what Azure will count");
    }

    [Fact]
    public void InputCostRatio_IgnoresAnEmptySample()
    {
        var ratio = new InputCostRatio();
        ratio.Observe(counted: 100, estimated: 1_000);

        ratio.Observe(counted: 0, estimated: 1_000);

        ratio.Value.Should().Be(0.1, "a call without usage must not move the ratio");
    }

    [Fact]
    public void InputCostRatio_ASmallCallThatMissedByAFactor_BarelyMovesALargeCallsRatio()
    {
        var ratio = new InputCostRatio();
        ratio.Observe(counted: 50_000, estimated: 40_000);

        ratio.Observe(counted: 5_000, estimated: 500);

        ratio.Value.Should().BeLessThan(1.4,
            "counted and estimated tokens are averaged apart, so a tool-schema-heavy first turn of 500 estimated "
            + "tokens cannot make the next 40k call reserve ten times its size");
    }

    internal static UsageDetails AnthropicUsage(long uncached, long cacheReads, long cacheWrites) => new()
    {
        InputTokenCount = uncached,
        AdditionalCounts = new AdditionalPropertiesDictionary<long>
        {
            ["CacheReadInputTokens"] = cacheReads,
            ["CacheCreationInputTokens"] = cacheWrites,
        },
    };

    private static Func<LlmQuotaSnapshot> Snapshotting(LlmRateLimiter limiter) =>
        () => limiter.Snapshot(new ModelQuotaScope("k", "p", "d"));
}
