using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
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

    [Fact]
    public void Track_CompactionPhase_AttributesSeparatelyAndCountsInTotal()
    {
        // Regression: the OpenAi compactor's own LLM call (~40-50k tokens per compaction
        // event in long runs) was logged but NOT added to the tracker — under-counting
        // run cost by the full compaction overhead. Fix mirrors the Claude-side pattern
        // (SetPhase("compaction") + Track + restore) so summarizer tokens land in a
        // dedicated phase and are summed into the run total.
        var tracker = new TokenUsageTracker();

        tracker.Track(CreateResponse(1000, 200, 0, 0));   // primary phase

        tracker.SetPhase("compaction");
        tracker.Track(46_713, 0);                         // summarizer call
        tracker.SetPhase("primary");

        tracker.Track(CreateResponse(800, 150, 0, 0));    // back to primary

        var summary = tracker.GetSummary();
        summary.TotalInputTokens.Should().Be(1000 + 46_713 + 800);
        summary.TotalOutputTokens.Should().Be(200 + 150);

        var phases = tracker.GetPhaseBreakdown();
        phases.Should().ContainKey("primary");
        phases.Should().ContainKey("compaction");
        phases["compaction"].InputTokens.Should().Be(46_713);
        phases["primary"].InputTokens.Should().Be(1000 + 800);
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

        config.IsEnabled.Should().BeTrue();
        config.Strategy.Should().Be("automatic");
    }

    [Fact]
    public void AgentConfig_HasCacheWithDefaults()
    {
        var agentConfig = new AgentConfig();

        agentConfig.Cache.Should().NotBeNull();
        agentConfig.Cache.IsEnabled.Should().BeTrue();
    }
}
