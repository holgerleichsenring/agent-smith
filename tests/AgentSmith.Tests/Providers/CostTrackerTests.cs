using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public class CostTrackerTests
{
    private static PricingConfig CreateTestPricing() => new()
    {
        Models = new Dictionary<string, ModelPricing>
        {
            ["claude-sonnet"] = new() { InputPerMillion = 3.0m, OutputPerMillion = 15.0m, CacheReadPerMillion = 0.30m },
            ["claude-haiku"] = new() { InputPerMillion = 0.80m, OutputPerMillion = 4.0m, CacheReadPerMillion = 0.08m }
        }
    };

    [Fact]
    public void CalculateCost_SinglePhase_CorrectCost()
    {
        var pricing = CreateTestPricing();
        var tracker = new TokenUsageTracker();
        var costTracker = new CostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        tracker.SetPhase("primary");
        tracker.Track(CreateResponse(1_000_000, 100_000, 0, 0));

        var summary = costTracker.CalculateCost(tracker);

        // 1M input × $3/MT + 100k output × $15/MT = $3 + $1.50 = $4.50
        summary.TotalCost.Should().Be(4.50m);
        summary.Phases["primary"].Cost.Should().Be(4.50m);
    }

    [Fact]
    public void CalculateCost_MultiplePhases_BreaksDownCorrectly()
    {
        var pricing = CreateTestPricing();
        var tracker = new TokenUsageTracker();
        var costTracker = new CostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("scout", "claude-haiku");
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        tracker.SetPhase("scout");
        tracker.Track(CreateResponse(100_000, 10_000, 0, 0));

        tracker.SetPhase("primary");
        tracker.Track(CreateResponse(100_000, 10_000, 0, 0));

        var summary = costTracker.CalculateCost(tracker);

        // Scout: 100k × $0.80/MT + 10k × $4/MT = $0.08 + $0.04 = $0.12
        summary.Phases["scout"].Cost.Should().Be(0.12m);
        summary.Phases["scout"].Model.Should().Be("claude-haiku");

        // Primary: 100k × $3/MT + 10k × $15/MT = $0.30 + $0.15 = $0.45
        summary.Phases["primary"].Cost.Should().Be(0.45m);
        summary.Phases["primary"].Model.Should().Be("claude-sonnet");

        summary.TotalCost.Should().Be(0.57m);
    }

    [Fact]
    public void CalculateCost_WithCacheRead_IncludesCacheReadCost()
    {
        var pricing = CreateTestPricing();
        var tracker = new TokenUsageTracker();
        var costTracker = new CostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        tracker.SetPhase("primary");
        tracker.Track(CreateResponse(50_000, 10_000, 0, 200_000));

        var summary = costTracker.CalculateCost(tracker);

        // 50k × $3/MT + 10k × $15/MT + 200k × $0.30/MT = $0.15 + $0.15 + $0.06 = $0.36
        summary.Phases["primary"].Cost.Should().Be(0.36m);
    }

    [Fact]
    public void CalculateCost_UnknownModel_ReturnsZeroCost()
    {
        var pricing = CreateTestPricing();
        var tracker = new TokenUsageTracker();
        var costTracker = new CostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "unknown-model");

        tracker.SetPhase("primary");
        tracker.Track(CreateResponse(100_000, 10_000, 0, 0));

        var summary = costTracker.CalculateCost(tracker);
        summary.TotalCost.Should().Be(0m);
    }

    [Fact]
    public void PhaseTracking_TrackerAccumulatesPerPhase()
    {
        var tracker = new TokenUsageTracker();

        tracker.SetPhase("scout");
        tracker.Track(CreateResponse(100, 50, 0, 0));
        tracker.Track(CreateResponse(200, 80, 0, 0));

        tracker.SetPhase("primary");
        tracker.Track(CreateResponse(500, 100, 0, 50));

        var phases = tracker.GetPhaseBreakdown();

        phases["scout"].InputTokens.Should().Be(300);
        phases["scout"].OutputTokens.Should().Be(130);
        phases["scout"].Iterations.Should().Be(2);

        phases["primary"].InputTokens.Should().Be(500);
        phases["primary"].OutputTokens.Should().Be(100);
        phases["primary"].CacheReadTokens.Should().Be(50);
        phases["primary"].Iterations.Should().Be(1);
    }

    [Fact]
    public void PricingConfig_DefaultsToEmpty()
    {
        var config = new PricingConfig();
        config.Models.Should().BeEmpty();
    }

    [Fact]
    public void AgentConfig_HasPricingWithDefaults()
    {
        var config = new AgentConfig();
        config.Pricing.Should().NotBeNull();
        config.Pricing.Models.Should().BeEmpty();
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
