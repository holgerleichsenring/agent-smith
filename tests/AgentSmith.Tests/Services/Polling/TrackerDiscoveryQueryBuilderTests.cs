using AgentSmith.Application.Services.Polling;
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
