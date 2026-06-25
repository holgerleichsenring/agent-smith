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
        PipelineFromLabel = new Dictionary<string, string> { ["agent-smith:bug"] = "fix-bug" },
    };

    [Fact]
    public void Apply_ProjectOmitsStatuses_InheritsTracker()
    {
        var project = new RawProjectEntry { Tracker = "t", Resolution = new() { ["tag"] = "Rheview" } };

        _builder.Apply("p", project, AdoTracker());

        var trigger = project.AzuredevopsTrigger!;
        trigger.TriggerStatuses.Should().BeEquivalentTo("New", "Active");
        trigger.DoneStatus.Should().Be("Resolved");
        trigger.FailedStatus.Should().Be("Resolved");
        trigger.PipelineFromLabel.Should().ContainKey("agent-smith:bug");
    }

    [Fact]
    public void Apply_ProjectOverridesStatus_ProjectWins()
    {
        var project = new RawProjectEntry
        {
            Tracker = "t",
            AzuredevopsTrigger = new WebhookTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = "X" },
                TriggerStatuses = ["Committed"],
            },
        };

        _builder.Apply("p", project, AdoTracker());

        project.AzuredevopsTrigger!.TriggerStatuses.Should().BeEquivalentTo("Committed");
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

    [Fact]
    public void Apply_LegacyFullWrapper_LoadsAndResolvesIdentically()
    {
        var legacy = new WebhookTriggerConfig
        {
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = "X" },
            TriggerStatuses = ["New", "Active"],
            DoneStatus = "Resolved",
            FailedStatus = "Resolved",
            PipelineFromLabel = new Dictionary<string, string> { ["agent-smith:bug"] = "fix-bug" },
        };
        var project = new RawProjectEntry { Tracker = "t", AzuredevopsTrigger = legacy };

        _builder.Apply("p", project, AdoTracker());

        project.AzuredevopsTrigger.Should().BeSameAs(legacy);
        legacy.ProjectResolution!.Value.Should().Be("X");
        legacy.DoneStatus.Should().Be("Resolved");
    }

    [Fact]
    public void Apply_NoTrackerNoResolution_LeavesTriggersNull()
    {
        var project = new RawProjectEntry { Tracker = "t" };

        _builder.Apply("p", project, new RawTrackerEntry { Type = TrackerType.AzureDevOps });

        project.AzuredevopsTrigger.Should().BeNull();
    }
}
