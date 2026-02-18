using AgentSmith.Contracts.Configuration;
using AgentSmith.Infrastructure.Providers.Agent;
using Anthropic.SDK.Messaging;
using FluentAssertions;

namespace AgentSmith.Tests.Providers;

public class TokenUsageTrackerTests
{
    [Fact]
    public void Track_AccumulatesTokens()
    {
        var tracker = new TokenUsageTracker();

        tracker.Track(CreateResponse(100, 50, 0, 0));
        tracker.Track(CreateResponse(80, 40, 0, 60));

        var summary = tracker.GetSummary();
        summary.TotalInputTokens.Should().Be(180);
        summary.TotalOutputTokens.Should().Be(90);
        summary.CacheReadTokens.Should().Be(60);
        summary.Iterations.Should().Be(2);
    }

    [Fact]
    public void CacheHitRate_CalculatesCorrectly()
    {
        var tracker = new TokenUsageTracker();

        // 100 input + 400 cache read = 80% cache hit rate
        tracker.Track(CreateResponse(100, 50, 0, 400));

        var summary = tracker.GetSummary();
        summary.CacheHitRate.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public void CacheHitRate_ZeroTokens_ReturnsZero()
    {
        var summary = new TokenUsageSummary(0, 0, 0, 0, 0);

        summary.CacheHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheHitRate_NoCacheReads_ReturnsZero()
    {
        var tracker = new TokenUsageTracker();
        tracker.Track(CreateResponse(500, 100, 200, 0));

        var summary = tracker.GetSummary();
        summary.CacheHitRate.Should().Be(0.0);
    }

    [Fact]
    public void Track_AccumulatesCacheCreationTokens()
    {
        var tracker = new TokenUsageTracker();

        tracker.Track(CreateResponse(100, 50, 300, 0));
        tracker.Track(CreateResponse(80, 40, 0, 300));

        var summary = tracker.GetSummary();
        summary.CacheCreationTokens.Should().Be(300);
        summary.CacheReadTokens.Should().Be(300);
    }

    private static MessageResponse CreateResponse(
        int input, int output, int cacheCreate, int cacheRead)
    {
        return new MessageResponse
        {
            Usage = new Usage
            {
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = cacheCreate,
                CacheReadInputTokens = cacheRead
            }
        };
    }
}

public class CacheConfigTests
{
    [Fact]
    public void Defaults_CacheEnabled()
    {
        var config = new CacheConfig();

        config.Enabled.Should().BeTrue();
        config.Strategy.Should().Be("automatic");
    }

    [Fact]
    public void AgentConfig_HasCacheWithDefaults()
    {
        var agentConfig = new AgentConfig();

        agentConfig.Cache.Should().NotBeNull();
        agentConfig.Cache.Enabled.Should().BeTrue();
    }
}
