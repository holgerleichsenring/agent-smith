using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Polling;

public sealed class TrackerDiscoveryQueryBuilderTests
{
    private static readonly TrackerDiscoveryQueryBuilder Builder =
        new(NullLogger<TrackerDiscoveryQueryBuilder>.Instance);

    [Fact]
    public void Build_AllTagProjects_OneBranchPerRoutedTrackerTrigger()
    {
        var tracker = Tracker("jira-main", TrackerType.Jira);
        var config = Config(tracker,
            JiraProject("alpha", tracker, "alpha-tag", ["To Do"]),
            JiraProject("beta", tracker, "beta-tag", ["In Progress"]));

        var query = Builder.Build(config, tracker);

        query.Branches.Should().HaveCount(2);
        query.Branches.Should().OnlyContain(b => b.Criterion!.Strategy == ResolutionStrategy.Tag);
    }

    [Fact]
    public void Build_SelectsThisTrackersTrigger_ByTrackerType()
    {
        var tracker = Tracker("gh", TrackerType.GitHub);
        var config = Config(tracker, GithubProject("p", tracker, "gh-tag", ["Open"]));

        var query = Builder.Build(config, tracker);

        query.Branches.Should().ContainSingle()
            .Which.Statuses.Should().BeEquivalentTo(new[] { "Open" });
    }

    [Fact]
    public void Build_EmptyTriggerStatuses_BranchIsStatusUnconstrained()
    {
        var tracker = Tracker("gh", TrackerType.GitHub);
        var config = Config(tracker, GithubProject("p", tracker, "gh-tag", []));

        var query = Builder.Build(config, tracker);

        query.Branches.Should().ContainSingle().Which.Statuses.Should().BeEmpty();
        query.Branches[0].Criterion!.Value.Should().Be("gh-tag");
    }

    /// <summary>
    /// 2026-09-22-766b: the server-side label guard is built from the union of every routed
    /// project's pipeline_from_label keys, and a ticket that HARD-BINDS never routes through that
    /// map — so without this the guard would filter the binding tickets out of discovery and they
    /// would never be polled at all. The guard must therefore name exactly what binds, which is
    /// two keys: the approval stamp every filing writes, and the phase word a person types.
    /// </summary>
    [Fact]
    public void Build_ALabelGuard_LetsBothPhaseExecutionBindingsThrough()
    {
        var tracker = Tracker("jira-main", TrackerType.Jira);
        var project = JiraProject("alpha", tracker, "alpha-tag", ["To Do"]);
        project.JiraTrigger!.PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "code" };

        var query = Builder.Build(Config(tracker, project), tracker);

        query.TriggerLabels.Should().BeEquivalentTo(
            ["bug", FiledTicketLabels.ApprovedSetStamp, PhaseTicketRenderer.PhaseLabel]);
    }

    [Fact]
    public void Build_NoLabelGuard_AddsNoBindingKeyOfItsOwn()
    {
        var tracker = Tracker("jira-main", TrackerType.Jira);

        var query = Builder.Build(
            Config(tracker, JiraProject("alpha", tracker, "alpha-tag", ["To Do"])), tracker);

        query.TriggerLabels.Should().BeEmpty(
            "a project with no pipeline_from_label filters nothing, and a guard naming only the "
            + "framework's keys would hide every ordinary ticket from the poll");
    }

    [Fact]
    public void Build_DoneAndFailedStatus_ParkingStatusesUnion()
    {
        var tracker = Tracker("jira-main", TrackerType.Jira);
        var project = JiraProject("alpha", tracker, "alpha-tag", ["To Do"]);
        project.JiraTrigger!.DoneStatus = "In Review";
        project.JiraTrigger!.FailedStatus = "Rejected";

        var query = Builder.Build(Config(tracker, project), tracker);

        query.ParkingStatuses.Should().BeEquivalentTo(new[] { "In Review", "Rejected" });
    }

    [Fact]
    public void Build_NeedsClarificationStatus_IncludedInParkingStatuses()
    {
        // p0318: a clarification-parked ticket sits in needs_clarification_status and must
        // be excluded from claimable discovery so it is not re-fetched + re-posted each poll.
        var tracker = Tracker("jira-main", TrackerType.Jira);
        var project = JiraProject("alpha", tracker, "alpha-tag", ["To Do"]);
        project.JiraTrigger!.DoneStatus = "In Review";
        project.JiraTrigger!.NeedsClarificationStatus = "Question";

        var query = Builder.Build(Config(tracker, project), tracker);

        query.ParkingStatuses.Should().Contain("Question");
    }

    [Fact]
    public void Build_OverMaxBranches_CollapsesToSingleBroadBranch()
    {
        var tracker = Tracker("jira-main", TrackerType.Jira);
        var projects = Enumerable.Range(0, 30)
            .Select(i => JiraProject($"p{i}", tracker, $"tag{i}", ["To Do"]))
            .ToArray();

        var query = Builder.Build(Config(tracker, projects), tracker);

        query.Branches.Should().ContainSingle();
        query.Branches[0].Statuses.Should().BeEmpty();
        query.Branches[0].Criterion.Should().BeNull();
    }

    private static TrackerConnection Tracker(string name, TrackerType type) =>
        new() { Name = name, Type = type };

    private static AgentSmithConfig Config(TrackerConnection tracker, params ResolvedProject[] projects) =>
        new()
        {
            Trackers = new Dictionary<string, TrackerConnection> { [tracker.Name] = tracker },
            Projects = projects.ToDictionary(p => p.Name),
        };

    private static ResolvedProject JiraProject(
        string name, TrackerConnection tracker, string tag, string[] statuses) =>
        new()
        {
            Name = name,
            Tracker = tracker,
            JiraTrigger = new JiraTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.Tag,
                    Value = tag,
                },
                TriggerStatuses = [.. statuses],
            },
        };

    private static ResolvedProject GithubProject(
        string name, TrackerConnection tracker, string tag, string[] statuses) =>
        new()
        {
            Name = name,
            Tracker = tracker,
            GithubTrigger = new WebhookTriggerConfig
            {
                ProjectResolution = new ProjectResolutionConfig
                {
                    Strategy = ResolutionStrategy.Tag,
                    Value = tag,
                },
                TriggerStatuses = [.. statuses],
            },
        };
}
