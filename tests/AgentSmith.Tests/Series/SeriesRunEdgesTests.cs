using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dcf: the kind of every requires edge inside a series, read from the per-repo stack a start is
/// decided by — stacked where a repo is cut from the prerequisite's branch, merge where the dependent is held on one,
/// unknown where the prerequisite has no green attempt, landed once it merged.</summary>
public sealed class SeriesRunEdgesTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true };

    [Fact]
    public async Task OfAsync_APrerequisiteThatIsARepoTip_IsStackedInThatRepo()
    {
        Worked("s1", "server");

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "s1"));

        edges.Should().Equal(new SeriesRunEdgeView("D", "S1", SeriesRunEdgeKinds.Stacked));
    }

    [Fact]
    public async Task OfAsync_TwoIndependentPrerequisitesInOneRepo_TheOneHeldOnNeedsAMerge()
    {
        Worked("a", "server");
        Worked("b", "server");

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "a"), Edge("d", "b"));

        edges.Select(e => (e.RequiredTicketId, e.Kind)).Should().Equal(
            [("A", SeriesRunEdgeKinds.Stacked), ("B", SeriesRunEdgeKinds.Merge)],
            "merging B leaves A as the server base — the stack's fewest merges");
    }

    [Fact]
    public async Task OfAsync_APrerequisiteWithoutAGreenAttempt_IsUnknown()
    {
        Running("n");

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "n"));

        edges.Should().ContainSingle().Which.Kind.Should().Be(SeriesRunEdgeKinds.Unknown);
    }

    [Fact]
    public async Task OfAsync_ADeeperPrerequisiteNotGreen_MakesTheEdgeUnknown_ForADependentThatNeverStarted()
    {
        _facts.Edges.Add(Edge("b", "a"));
        Worked("b", "server");
        Running("a");

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "b"));

        edges.Should().ContainSingle().Which.Kind.Should().Be(SeriesRunEdgeKinds.Unknown, "the start waits on A, a base it may need");
    }

    [Fact]
    public async Task OfAsync_AStartedDependent_IsNeverUnknown_ItsStackAlone_Answers()
    {
        Running("n");
        _attempts.Setup(a => a.AttemptedAsync(Project, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string> { "D" });

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "n"));

        edges.Should().ContainSingle().Which.Kind.Should().Be(SeriesRunEdgeKinds.Stacked, "a started dependent is never held again");
    }

    [Fact]
    public async Task OfAsync_ALandedPrerequisite_IsLanded_AndOnMergeEveryOtherNeedsAMerge()
    {
        Worked("s1", "server");
        Worked("s2", "server");

        var edges = await EdgesAsync(RequiresStart.OnMerge, Edge("d", "s1"), Edge("d", "s2"), landed: ["S1"]);

        edges.Select(e => (e.RequiredTicketId, e.Kind)).Should().Equal(
            [("S1", SeriesRunEdgeKinds.Landed), ("S2", SeriesRunEdgeKinds.Merge)]);
    }

    [Fact]
    public async Task OfAsync_APrerequisiteOutsideTheSeries_IsNotAnEdgeOfIt_ButStillShapesTheStack()
    {
        Worked("a", "server");
        Worked("outside", "server");

        var edges = await EdgesAsync(RequiresStart.OnGreenPush, Edge("d", "a"), Edge("d", "outside"), series: ["D", "A"]);

        edges.Select(e => (e.RequiredTicketId, e.Kind)).Should().Equal([("A", SeriesRunEdgeKinds.Stacked)],
            "the outside ticket is the one held on; A stays a stacked base once it merges");
    }

    private async Task<IReadOnlyList<SeriesRunEdgeView>> EdgesAsync(
        RequiresStart mode, TicketRequirementEdge first, TicketRequirementEdge? second = null,
        IReadOnlyList<string>? landed = null, IReadOnlyList<string>? series = null)
    {
        IReadOnlyList<TicketRequirementEdge> edges = second is null ? [first] : [first, second];
        _facts.Edges.AddRange(edges);
        foreach (var id in landed ?? []) _facts.Land(id.ToLowerInvariant());
        var project = new ResolvedProject { Name = Project, Tracker = new TrackerConnection { Name = Tracker }, RequiresStart = mode };
        var facts = new SeriesRunProjectFacts(project, new Dictionary<string, IReadOnlyList<string>>(),
            (landed ?? []).ToHashSet(StringComparer.Ordinal), Array.Empty<PrReviewRecordView>().ToLookup(r => r.TicketId),
            new Dictionary<string, PreviousAttempt>()) { Edges = new Dictionary<string, IReadOnlyList<TicketRequirementEdge>> { ["D"] = edges } };
        var green = new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts);
        return await new SeriesRunEdges(green).OfAsync(facts, series ?? ["D", .. edges.Select(e => e.RequiredTicketId)], default);
    }

    private void Running(string ticket) =>
        _attempts.Setup(a => a.LatestAsync(Project, ticket.ToUpperInvariant(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt($"run-{ticket}", RunStatuses.Running, DateTimeOffset.UnixEpoch, Finished: false));

    private void Worked(string ticket, params string[] repos) =>
        _attempts.Setup(a => a.LatestAsync(Project, ticket.ToUpperInvariant(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt($"run-{ticket}", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true,
                PullRequestUrls = repos.ToDictionary(r => r, r => $"https://git.test/o/{r}/pull/{ticket}"),
            });
}
