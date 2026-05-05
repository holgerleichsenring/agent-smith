using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.Providers.Agent.Cost;
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
            ["claude-haiku"] = new() { InputPerMillion = 0.80m, OutputPerMillion = 4.0m, CacheReadPerMillion = 0.08m },
            ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m, CacheReadPerMillion = 0.50m }
        }
    };

    [Fact]
    public void ClaudeCostTracker_SinglePhase_CorrectCost()
    {
        var pricing = CreateTestPricing();
        var costTracker = new ClaudeCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(1_000_000, 100_000, 0, 0));

        var summary = costTracker.CalculateCost();

        // 1M input × $3/MT + 100k output × $15/MT = $3 + $1.50 = $4.50
        summary.TotalCost.Should().Be(4.50m);
        summary.Phases["primary"].Cost.Should().Be(4.50m);
    }

    [Fact]
    public void ClaudeCostTracker_MultiplePhases_BreaksDownCorrectly()
    {
        var pricing = CreateTestPricing();
        var costTracker = new ClaudeCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("scout", "claude-haiku");
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        costTracker.SetPhase("scout");
        costTracker.Track(CreateClaudeResponse(100_000, 10_000, 0, 0));

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(100_000, 10_000, 0, 0));

        var summary = costTracker.CalculateCost();

        summary.Phases["scout"].Cost.Should().Be(0.12m);
        summary.Phases["scout"].Model.Should().Be("claude-haiku");
        summary.Phases["primary"].Cost.Should().Be(0.45m);
        summary.Phases["primary"].Model.Should().Be("claude-sonnet");
        summary.TotalCost.Should().Be(0.57m);
    }

    [Fact]
    public void ClaudeCostTracker_WithCacheRead_IncludesCacheReadCost()
    {
        var pricing = CreateTestPricing();
        var costTracker = new ClaudeCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(50_000, 10_000, 0, 200_000));

        var summary = costTracker.CalculateCost();

        // 50k × $3/MT + 10k × $15/MT + 200k × $0.30/MT = $0.15 + $0.15 + $0.06 = $0.36
        summary.Phases["primary"].Cost.Should().Be(0.36m);
    }

    [Fact]
    public void ClaudeCostTracker_UnknownModel_ReturnsZeroCost()
    {
        var pricing = CreateTestPricing();
        var costTracker = new ClaudeCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "unknown-model");

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(100_000, 10_000, 0, 0));

        var summary = costTracker.CalculateCost();
        summary.TotalCost.Should().Be(0m);
    }

    [Fact]
    public void ClaudeCostTracker_EmptyPricing_AggregatesTokensReturnsZeroCost()
    {
        var costTracker = new ClaudeCostTracker(new PricingConfig(), NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "any-model");

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(100, 50, 0, 0));

        costTracker.GetTokenSummary().TotalInputTokens.Should().Be(100);
        costTracker.CalculateCost().TotalCost.Should().Be(0m);
    }

    [Fact]
    public void OpenAiCostTracker_NoCachedTokens_BillsFullInputAtFullRate()
    {
        var pricing = CreateTestPricing();
        var costTracker = new OpenAiCostTracker(pricing, NullLogger.Instance);
        var tokenTracker = costTracker.TokenTracker;
        costTracker.SetPhaseModel("primary", "gpt-4.1");

        costTracker.SetPhase("primary");
        // No cached tokens path: InputTokenDetails is null in this minimal stub.
        // Aggregate manually to simulate the no-cache path that the SDK would produce.
        tokenTracker.Track(1_000_000, 100_000, 0, 0);

        var summary = costTracker.CalculateCost();

        // 1M × $2/MT + 100k × $8/MT = $2.00 + $0.80 = $2.80
        summary.TotalCost.Should().Be(2.80m);
    }

    [Fact]
    public void OpenAiCostTracker_WithCachedTokens_BillsCachedAtCacheRate()
    {
        var pricing = CreateTestPricing();
        var costTracker = new OpenAiCostTracker(pricing, NullLogger.Instance);
        var tokenTracker = costTracker.TokenTracker;
        costTracker.SetPhaseModel("primary", "gpt-4.1");

        costTracker.SetPhase("primary");
        // Simulate what OpenAiCostTracker.Track(ChatCompletion) does: split prompt_tokens
        // (here 1M total) into 800k cached + 200k billable.
        // We feed the canonical split through TokenTracker directly because building
        // a real OpenAI ChatCompletion stub is impractical (sealed types, internal ctors).
        const int billable = 200_000;
        const int cached = 800_000;
        tokenTracker.Track(billable, 100_000, 0, cached);

        var summary = costTracker.CalculateCost();

        // billable: 200k × $2/MT = $0.40
        // cached:   800k × $0.50/MT = $0.40
        // output:   100k × $8/MT = $0.80
        // total = $1.60 (vs $2.80 if cached were billed at full rate — 43% saving)
        summary.TotalCost.Should().Be(1.60m);
        summary.Phases["primary"].CacheReadTokens.Should().Be(800_000);
        summary.Phases["primary"].InputTokens.Should().Be(200_000);
    }

    [Fact]
    public void GeminiCostTracker_TracksRawIntsNoCacheInfo()
    {
        var pricing = CreateTestPricing();
        var costTracker = new GeminiCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "gpt-4.1");

        costTracker.SetPhase("primary");
        costTracker.Track(100_000, 10_000);

        var summary = costTracker.CalculateCost();

        // 100k × $2/MT + 10k × $8/MT = $0.20 + $0.08 = $0.28
        summary.TotalCost.Should().Be(0.28m);
        summary.Phases["primary"].CacheReadTokens.Should().Be(0);
    }

    [Fact]
    public void OllamaCostTracker_ZeroPricing_StillAggregatesTokens()
    {
        var pricing = new PricingConfig
        {
            Models = new Dictionary<string, ModelPricing>
            {
                ["llama3"] = new() { InputPerMillion = 0m, OutputPerMillion = 0m }
            }
        };
        var costTracker = new OllamaCostTracker(pricing, NullLogger.Instance);
        costTracker.SetPhaseModel("primary", "llama3");

        costTracker.SetPhase("primary");
        costTracker.Track(50_000, 5_000);

        var summary = costTracker.CalculateCost();

        summary.TotalCost.Should().Be(0m);
        costTracker.GetTokenSummary().TotalInputTokens.Should().Be(50_000);
        costTracker.GetTokenSummary().TotalOutputTokens.Should().Be(5_000);
    }

    [Fact]
    public void PhaseTracking_ClaudeTrackerAccumulatesPerPhase()
    {
        var costTracker = new ClaudeCostTracker(new PricingConfig(), NullLogger.Instance);

        costTracker.SetPhase("scout");
        costTracker.Track(CreateClaudeResponse(100, 50, 0, 0));
        costTracker.Track(CreateClaudeResponse(200, 80, 0, 0));

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(500, 100, 0, 50));

        var phases = costTracker.GetPhaseBreakdown();

        phases["scout"].InputTokens.Should().Be(300);
        phases["scout"].OutputTokens.Should().Be(130);
        phases["scout"].Iterations.Should().Be(2);

        phases["primary"].InputTokens.Should().Be(500);
        phases["primary"].OutputTokens.Should().Be(100);
        phases["primary"].CacheReadTokens.Should().Be(50);
        phases["primary"].Iterations.Should().Be(1);
    }

    [Fact]
    public void TokenUsageTrackerSharing_CostTrackerWrapsExistingTokenTracker()
    {
        var sharedTracker = new TokenUsageTracker();
        var costTracker = new ClaudeCostTracker(CreateTestPricing(), NullLogger.Instance, sharedTracker);
        costTracker.SetPhaseModel("primary", "claude-sonnet");

        costTracker.SetPhase("primary");
        costTracker.Track(CreateClaudeResponse(100_000, 10_000, 0, 0));

        // Both views see the same data because cost tracker wraps the shared token tracker.
        sharedTracker.GetSummary().TotalInputTokens.Should().Be(100_000);
        costTracker.GetTokenSummary().TotalInputTokens.Should().Be(100_000);
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

    private static MessageResponse CreateClaudeResponse(
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
