using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0176a: per-repo cost attribution surfaces a non-null PerRepo summary
/// when any CallCostRecord carried a RepoName, and falls back to flat
/// phase-only grouping for single-repo / legacy pipelines.
/// </summary>
public sealed class PipelineCostTrackerPerRepoTests
{
    [Fact]
    public void BeginCallWithRepoName_BuildSummary_GroupsRecordsByRepoAndPhase()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });

        using (var scope = tracker.BeginCall("project-bootstrap", "producer", SkillExecutionPhase.Bootstrap, "repo-a"))
        {
            tracker.Track(BuildResponse("gpt-4.1", input: 500_000, output: 500_000));
        }
        using (var scope = tracker.BeginCall("project-bootstrap", "producer", SkillExecutionPhase.Bootstrap, "repo-b"))
        {
            tracker.Track(BuildResponse("gpt-4.1", input: 1_000_000, output: 1_000_000));
        }

        var summary = tracker.BuildSummary();
        summary.Should().NotBeNull();
        summary!.PerRepo.Should().NotBeNull();
        summary.PerRepo!.Keys.Should().BeEquivalentTo("repo-a", "repo-b");
        summary.PerRepo["repo-a"].TotalCost.Should().Be(0.5m * 2.0m + 0.5m * 8.0m);
        summary.PerRepo["repo-b"].TotalCost.Should().Be(1.0m * 2.0m + 1.0m * 8.0m);
        summary.TotalCost.Should().Be(summary.PerRepo["repo-a"].TotalCost + summary.PerRepo["repo-b"].TotalCost);
    }

    [Fact]
    public void BeginCallWithoutRepoName_BuildSummary_PerRepoIsNull()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });
        using (var scope = tracker.BeginCall("plan", "planner", SkillExecutionPhase.Plan))
        {
            tracker.Track(BuildResponse("gpt-4.1", input: 100_000, output: 100_000));
        }

        var summary = tracker.BuildSummary();
        summary!.PerRepo.Should().BeNull();
        summary.Phases.Should().ContainKey("Plan");
    }

    [Fact]
    public void BeginCallSomeWithRepoName_BuildSummary_PerRepoContainsOnlyAttributedRecords()
    {
        var tracker = new PipelineCostTracker(config: new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });
        using (var scope = tracker.BeginCall("plan", "planner", SkillExecutionPhase.Plan))
        {
            tracker.Track(BuildResponse("gpt-4.1", input: 100_000, output: 100_000));
        }
        using (var scope = tracker.BeginCall("project-bootstrap", "producer", SkillExecutionPhase.Bootstrap, "repo-a"))
        {
            tracker.Track(BuildResponse("gpt-4.1", input: 1_000_000, output: 1_000_000));
        }

        var summary = tracker.BuildSummary();
        summary!.PerRepo.Should().NotBeNull();
        summary.PerRepo!.Keys.Should().BeEquivalentTo("repo-a");
        // pipeline total still includes the unattributed Plan call
        summary.TotalCost.Should().BeGreaterThan(summary.PerRepo["repo-a"].TotalCost);
    }

    private static ChatResponse BuildResponse(string modelId, long input, long output) =>
        new(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = modelId,
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output },
        };
}
