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
    [Fact]
    public void ResolveFor_NullPipelineName_ReturnsDefault()
    {
        var config = new PipelineCostCapConfig
        {
            Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
            PerPipeline = new Dictionary<string, CostCapValues> { ["api-security-scan"] = new() { Usd = 10m, Tokens = 1_000_000 } }
        };

        config.ResolveFor(null).Should().BeSameAs(config.Default);
    }

    [Fact]
    public void ResolveFor_UnknownPipeline_ReturnsDefault()
    {
        var config = new PipelineCostCapConfig
        {
            Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
        };

        config.ResolveFor("unknown-pipeline").Should().BeSameAs(config.Default);
    }

    [Fact]
    public void ResolveFor_KnownPipeline_ReturnsOverride()
    {
        var overrideValues = new CostCapValues { Usd = 10m, Tokens = 1_000_000 };
        var config = new PipelineCostCapConfig
        {
            Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
            PerPipeline = new Dictionary<string, CostCapValues> { ["api-security-scan"] = overrideValues }
        };

        config.ResolveFor("api-security-scan").Should().BeSameAs(overrideValues);
    }

    [Fact]
    public void ResolveFor_CaseInsensitiveMatch()
    {
        var overrideValues = new CostCapValues { Usd = 2m, Tokens = 200_000 };
        var config = new PipelineCostCapConfig
        {
            PerPipeline = new Dictionary<string, CostCapValues>(StringComparer.OrdinalIgnoreCase) { ["fix-bug"] = overrideValues }
        };

        config.ResolveFor("FIX-BUG").Should().BeSameAs(overrideValues);
    }

    [Fact]
    public void Default_Values_AreFiveUsdAndFiveHundredThousandTokens()
    {
        var config = new PipelineCostCapConfig();

        config.Default.Usd.Should().Be(5.0m);
        config.Default.Tokens.Should().Be(500_000);
    }
}
