using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Configuration;

public class EffectiveTriggerBuilderTests
{
    private readonly EffectiveTriggerBuilder _builder = new();

    private static RawTrackerEntry AdoTracker() => new()
    {
        Type = TrackerType.AzureDevOps,
        TriggerStatuses = ["New", "Active"],
        DoneStatus = "Resolved",
        FailedStatus = "Resolved",
        PipelineFromLabel = new Dictionary<string, string> { ["agent-smith:bug"] = "code" },
    };

    [Fact]
    public void Apply_ProjectOmitsStatuses_InheritsTracker()
    {
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, AdoTracker());

        var trigger = project.AzuredevopsTrigger!;
        trigger.TriggerStatuses.Should().BeEquivalentTo("New", "Active");
        trigger.DoneStatus.Should().Be("Resolved");
        trigger.FailedStatus.Should().Be("Resolved");
        trigger.PipelineFromLabel.Should().ContainKey("agent-smith:bug");
    }

    // 2026-10-29-6bd3a: the fields a per-project trigger block used to carry come from the tracker.
    [Fact]
    public void Apply_TheTrackersTriggerFields_ReachTheEffectiveTrigger()
    {
        var tracker = AdoTracker();
        tracker.CommentKeyword = "@agent-smith";
        tracker.PrTriggerLabel = "please-review";
        tracker.InProgressStatus = "Active";
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, tracker);

        project.AzuredevopsTrigger!.CommentKeyword.Should().Be("@agent-smith");
        project.AzuredevopsTrigger.PrTriggerLabel.Should().Be("please-review");
        project.AzuredevopsTrigger.InProgressStatus.Should().Be("Active");
    }

    [Fact]
    public void Apply_JiraTrackerWithoutAssignee_KeepsAgentSmith()
    {
        var tracker = new RawTrackerEntry { Type = TrackerType.Jira, OpenStates = ["To Do"] };
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, tracker);

        project.JiraTrigger!.AssigneeName.Should().Be("Agent Smith",
            "a project that never had a block always started on an issue assigned to Agent Smith");
        project.JiraTrigger.Secret.Should().BeNull();
    }

    [Fact]
    public void Apply_JiraTrackerAssigneeAndSecret_ReachTheJiraTrigger()
    {
        var tracker = new RawTrackerEntry
        {
            Type = TrackerType.Jira, OpenStates = ["To Do"], AssigneeName = "Bot", WebhookSecret = "s3cret",
        };
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, tracker);

        project.JiraTrigger!.AssigneeName.Should().Be("Bot");
        project.JiraTrigger.Secret.Should().Be("s3cret");
    }

    [Theory]
    [InlineData("tag", ResolutionStrategy.Tag)]
    [InlineData("area_path", ResolutionStrategy.AreaPath)]
    [InlineData("repo", ResolutionStrategy.Repo)]
    [InlineData("to_address", ResolutionStrategy.ToAddress)]
    public void Apply_ResolutionShorthand_ParsesStrategyFromKey(string key, ResolutionStrategy expected)
    {
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { [key] = "v" } };

        _builder.Apply("p", project, AdoTracker());

        project.AzuredevopsTrigger!.ProjectResolution!.Strategy.Should().Be(expected);
        project.AzuredevopsTrigger.ProjectResolution.Value.Should().Be("v");
    }

    [Theory]
    [InlineData(TrackerType.AzureDevOps)]
    [InlineData(TrackerType.GitHub)]
    [InlineData(TrackerType.GitLab)]
    [InlineData(TrackerType.Jira)]
    public void Apply_TriggerType_InferredFromTrackerType(TrackerType type)
    {
        var tracker = new RawTrackerEntry { Type = type, TriggerStatuses = ["New"] };
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "v" } };

        _builder.Apply("p", project, tracker);

        var declared = new[]
        {
            project.AzuredevopsTrigger is not null,
            project.GithubTrigger is not null,
            project.GitlabTrigger is not null,
            project.JiraTrigger is not null,
        };
        declared.Count(x => x).Should().Be(1, "exactly the tracker-typed trigger is populated");
    }

    [Fact]
    public void Apply_TrackerHasOnlyOpenStates_FallsBackToOpenStatesForTriggerGate()
    {
        var tracker = new RawTrackerEntry { Type = TrackerType.AzureDevOps, OpenStates = ["New", "Active"] };
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "v" } };

        _builder.Apply("p", project, tracker);

        project.AzuredevopsTrigger!.TriggerStatuses.Should().BeEquivalentTo("New", "Active");
    }

    [Fact]
    public void Apply_UnknownResolutionStrategy_Throws()
    {
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["bogus"] = "v" } };

        var act = () => _builder.Apply("p", project, AdoTracker());

        act.Should().Throw<ConfigurationException>().WithMessage("*not a known strategy*");
    }

    // 2026-09-16-a4d7: the label map and the answer for "nothing matched" are one
    // decision, so the tracker owns both and the merge copies both.
    [Fact]
    public void Tracker_DeclaresDefaultPipeline_FillsTheTriggerWrapper()
    {
        var tracker = AdoTracker();
        tracker.DefaultPipeline = "security-scan";
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, tracker);

        project.AzuredevopsTrigger!.DefaultPipeline.Should().Be("security-scan");
    }

    [Fact]
    public void Tracker_DeclaresNothing_BehavesExactlyAsBefore()
    {
        // No tracker default and no project default: the wrapper states none, and the
        // resolver — the one place that answers — still answers fix-bug. No label map
        // either, because a map turns an unmatched ticket into a drop, not a fallback.
        var tracker = AdoTracker();
        tracker.PipelineFromLabel = null;
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "checkout" } };

        _builder.Apply("p", project, tracker);

        var trigger = project.AzuredevopsTrigger!;
        trigger.DefaultPipeline.Should().BeNull();
        new PipelineResolver().Resolve(trigger, []).Should().Be(PipelinePresets.UndeclaredFallbackPipeline);
    }

    [Fact]
    public void Apply_NoTrackerNoResolution_LeavesTriggersNull()
    {
        var project = new RawProjectEntry { Tracker = "t" };

        _builder.Apply("p", project, new RawTrackerEntry { Type = TrackerType.AzureDevOps });

        project.AzuredevopsTrigger.Should().BeNull();
    }
}
