using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class PipelineCostTrackerCapTests
{
    private static ChatResponse Make(int input, int output, string model = "gpt-4.1") =>
        new(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = model,
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output }
        };

    [Fact]
    public void IsBudgetExhausted_NoCapConfigured_AlwaysFalse()
    {
        var tracker = new PipelineCostTracker();
        tracker.Track(Make(input: 10_000_000, output: 10_000_000));

        tracker.IsBudgetExhausted.Should().BeFalse();
    }

    [Fact]
    public void IsBudgetExhausted_BelowCap_False()
    {
        var tracker = new PipelineCostTracker(
            config: null,
            costCap: new CostCapValues { Usd = 5.0m, Tokens = 500_000 });
        tracker.Track(Make(input: 100, output: 50));

        tracker.IsBudgetExhausted.Should().BeFalse();
    }

    [Fact]
    public void IsBudgetExhausted_TokensCapExceeded_True()
    {
        var tracker = new PipelineCostTracker(
            config: null,
            costCap: new CostCapValues { Usd = 100.0m, Tokens = 1_000 });
        tracker.Track(Make(input: 800, output: 500));

        tracker.IsBudgetExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsBudgetExhausted_UsdCapExceeded_True()
    {
        var tracker = new PipelineCostTracker(
            config: null,
            costCap: new CostCapValues { Usd = 0.0001m, Tokens = 100_000_000 });
        tracker.Track(Make(input: 100_000, output: 100_000));

        tracker.IsBudgetExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsBudgetExhausted_FlipsAfterCrossingThreshold()
    {
        var tracker = new PipelineCostTracker(
            config: null,
            costCap: new CostCapValues { Usd = 100.0m, Tokens = 1_500 });

        tracker.Track(Make(input: 500, output: 500));
        tracker.IsBudgetExhausted.Should().BeFalse();

        tracker.Track(Make(input: 800, output: 100));
        tracker.IsBudgetExhausted.Should().BeTrue();
    }
}

public sealed class PipelineCostCapConfigTests
{
    // p0270a: cost-cap RESOLUTION (per-pipeline override ?? default, case-insensitive)
    // moved out of PipelineCostCapConfig.ResolveFor into the single
    // ConfigResolutionPass — pinned in ConfigResolutionPassTests.ResolveCostCap_*.
    // This class keeps only the data-shape default assertions.

    [Fact]
    public void Default_Values_AreFiveUsdAndFiveHundredThousandTokens()
    {
        var config = new PipelineCostCapConfig();

        config.Default.Usd.Should().Be(5.0m);
        config.Default.Tokens.Should().Be(500_000);
    }
}
