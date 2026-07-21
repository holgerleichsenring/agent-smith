using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0361: cost display must be trustworthy. Unknown models must never price
/// at $0 silently, the default table must know the current Claude generation,
/// and the per-phase breakdown must price each call at its own model so the
/// phase rows sum to the headline accrual.
/// </summary>
public sealed class PipelineCostTrackerCostTruthTests
{
    [Fact]
    public void Resolve_CurrentClaudeAliases_HaveCurrentPrices()
    {
        var resolver = new ModelPricingResolver();

        resolver.Resolve("claude-sonnet-5")!.InputPerMillion.Should().Be(3.0m);
        resolver.Resolve("claude-opus-4-8")!.OutputPerMillion.Should().Be(25.0m);
        resolver.Resolve("claude-fable-5")!.InputPerMillion.Should().Be(10.0m);
        // Dated snapshot ids resolve via alias prefix; Haiku 4.5 is $1/$5
        // (the old table carried Haiku 3.5's 0.80/4.0).
        var haiku = resolver.Resolve("claude-haiku-4-5-20251001")!;
        haiku.InputPerMillion.Should().Be(1.0m);
        haiku.OutputPerMillion.Should().Be(5.0m);
    }

    [Fact]
    public void Track_UnknownModel_AccruesUnpricedTokens_AndSummaryCarriesThem()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });

        tracker.Track(BuildResponse("brand-new-model", input: 500_000, output: 100_000));

        tracker.UnpricedTokensByModel.Should().ContainKey("brand-new-model")
            .WhoseValue.Should().Be(600_000);
        tracker.ToString().Should().Contain("COST INCOMPLETE")
            .And.Contain("brand-new-model");

        var summary = tracker.BuildSummary();
        summary!.UnpricedTokensByModel.Should().NotBeNull();
        summary.UnpricedTokensByModel!["brand-new-model"].Should().Be(600_000);
    }

    [Fact]
    public void Track_KnownModel_LeavesUnpricedEmpty_AndSummaryOmitsIt()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });

        tracker.Track(BuildResponse("gpt-4.1", input: 1_000, output: 1_000));

        tracker.UnpricedTokensByModel.Should().BeEmpty();
        tracker.BuildSummary()!.UnpricedTokensByModel.Should().BeNull();
        tracker.ToString().Should().NotContain("COST INCOMPLETE");
    }

    [Fact]
    public void BuildSummary_MixedModels_PhaseCostsSumToHeadlineAccrual()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new()
            {
                ["expensive"] = new() { InputPerMillion = 10.0m, OutputPerMillion = 30.0m },
                ["cheap"] = new() { InputPerMillion = 0.10m, OutputPerMillion = 0.40m }
            }
        });

        using (var scope = tracker.BeginCall("master", "coder", SkillExecutionPhase.Implementation))
        {
            tracker.Track(BuildResponse("expensive", input: 1_000_000, output: 0)); // $10
            tracker.Track(BuildResponse("cheap", input: 1_000_000, output: 0));     // $0.10 — and last
            tracker.EndCall(scope, null);
        }

        var summary = tracker.BuildSummary()!;
        var implementation = summary.Phases[nameof(SkillExecutionPhase.Implementation)];

        // The old breakdown re-priced the whole phase at the LAST model
        // ("cheap") — $0.20 for a run that really cost $10.10.
        implementation.Cost.Should().Be(10.10m);
        implementation.Model.Should().Be("cheap+expensive");
        summary.Phases.Values.Sum(p => p.Cost).Should().Be(tracker.EstimateCostUsd());
    }

    [Fact]
    public void BuildRecord_CarriesDuplicateToolCallCount()
    {
        var tracker = new PipelineCostTracker();
        var scope = tracker.BeginCall("master", "coder", SkillExecutionPhase.Implementation);
        scope.SetDuplicateToolCalls(3);
        tracker.EndCall(scope, null);

        tracker.PerSkillBreakdown.Single().DuplicateToolCallCount.Should().Be(3);
    }

    [Fact]
    public void RegisterToolCall_SameArgs_IncrementsOccurrence()
    {
        var scope = new CallScope("coder", "Implementation", null);

        scope.RegisterToolCall("read_file", "abc123").Should().Be(1);
        scope.RegisterToolCall("read_file", "abc123").Should().Be(2);
        scope.RegisterToolCall("read_file", "abc123").Should().Be(3);
        scope.DuplicateToolCallCount.Should().Be(2);
    }

    [Fact]
    public void RegisterToolCall_DifferentArgsOrTool_NoDuplicate()
    {
        var scope = new CallScope("coder", "Implementation", null);

        scope.RegisterToolCall("read_file", "abc123").Should().Be(1);
        scope.RegisterToolCall("read_file", "def456").Should().Be(1);
        scope.RegisterToolCall("write_file", "abc123").Should().Be(1);
        scope.DuplicateToolCallCount.Should().Be(0);
    }

    private static ChatResponse BuildResponse(string modelId, int input, int output) => new()
    {
        ModelId = modelId,
        Usage = new UsageDetails
        {
            InputTokenCount = input,
            OutputTokenCount = output
        }
    };
}
