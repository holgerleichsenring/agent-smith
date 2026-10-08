using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>2026-10-08-9e6e: the change sweep — sources, cursors, rotation — nudges only.</summary>
public sealed class ChangeSweepTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly SettableClock _clock = new() { Now = Noon };
    private readonly ServerStateStore _store;
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkNudges> _nudges = new();
    private readonly Mock<ITicketProvider> _tracker = new();
    private readonly Mock<ISourceProvider> _source = new();

    public ChangeSweepTests()
    {
        _store = new ServerStateStore(_clock);
        _tracker.As<IChangedTicketLister>();
        _source.As<IChangedPullRequestLister>();
    }

    public void Dispose() => _store.Dispose();

    private static TrackerConnection Tracker => new() { Name = "jira", Type = TrackerType.Jira, Polling = new PollingConfig { Enabled = true } };

    private static AgentSmithConfig Config(params string[] projects) => new()
    {
        Projects = projects.ToDictionary(p => p, p => new ResolvedProject
        {
            Name = p, Tracker = Tracker, Repos = [new RepoConnection { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub }],
        }),
    };

    private IReadOnlyList<SweepSource> Sources(AgentSmithConfig config)
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(_tracker.Object);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_source.Object);
        return new ChangeSweepSources(tickets.Object, sources.Object, new ConfiguredRepoFinder(), _attempts.Object, _nudges.Object)
            .For(config, [Tracker]);
    }

    private void Ran(string project, string ticket) => _attempts.Setup(a => a.LatestAsync(project, ticket, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PreviousAttempt("run-1", "success", Noon.AddDays(-1), true));

    private void VerifyNudged(string project, string ticket, Times times) =>
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Project == project && r.TicketId == ticket
            && r.Origin == ReworkNudgeOrigin.Sweep), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Sweep_ChangedTicketWithRun_Nudged()
    {
        Ran("p", "A-1");
        var tickets = Sources(Config("p")).Single(s => s.Key == "tickets:jira");

        await tickets.NudgeAsync(new ChangedItem(Noon, TicketId: "A-1"), CancellationToken.None);
        await tickets.NudgeAsync(new ChangedItem(Noon, TicketId: "A-2"), CancellationToken.None);

        VerifyNudged("p", "A-1", Times.Once());
        VerifyNudged("p", "A-2", Times.Never());
    }

    [Fact]
    public async Task Sweep_ForkPr_NotNudged()
    {
        Ran("p", "12");
        var prs = Sources(Config("p")).Single(s => s.Key.StartsWith("pr:", StringComparison.Ordinal));

        await prs.NudgeAsync(new ChangedItem(Noon, PrUrl: "https://github.com/o/r/pull/4", HeadRef: "agent-smith/12", SameRepository: false), CancellationToken.None);

        _nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sweep_AmbiguousOwner_NotNudged()
    {
        Ran("p", "12");
        Ran("q", "12");
        var prs = Sources(Config("p", "q")).Single(s => s.Key.StartsWith("pr:", StringComparison.Ordinal));

        await prs.NudgeAsync(new ChangedItem(Noon, PrUrl: "https://github.com/o/r/pull/4", HeadRef: "agent-smith/12"), CancellationToken.None);

        _nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sweep_AzureDevOpsMinusFive_NudgedOncePerVote()
    {
        Ran("p", "12");
        var prs = Sources(Config("p")).Single(s => s.Key.StartsWith("pr:", StringComparison.Ordinal));
        var vote = new ChangedItem(Noon, PrUrl: "https://dev.azure.com/o/p/_git/r/pullrequest/9", HeadRef: "refs/heads/agent-smith/12", Dedupe: "9|v-1");

        await prs.NudgeAsync(vote, CancellationToken.None);
        await prs.NudgeAsync(vote, CancellationToken.None);
        await prs.NudgeAsync(vote with { Dedupe = "9|v-1,v-2" }, CancellationToken.None);

        VerifyNudged("p", "12", Times.Exactly(2));
    }

    [Fact]
    public void Sweep_BudgetCut_ResumesFromLastItem()
    {
        var from = new SweepPosition(Noon.AddHours(-1));
        var cut = new ChangedPage([new ChangedItem(Noon.AddMinutes(-40)), new ChangedItem(Noon.AddMinutes(-30))], Cut: true);

        SweepCursorPolicy.After(from, cut, Noon).At.Should().Be(Noon.AddMinutes(-30));
        SweepCursorPolicy.After(from, cut with { Resume = "page-3" }, Noon).Should().Be(new SweepPosition(from.At, "page-3"));
    }

    [Fact]
    public void Sweep_IdleCursor_NoRepeatNudge() =>
        SweepCursorPolicy.After(new SweepPosition(Noon.AddHours(-1)), new ChangedPage([]), Noon).At
            .Should().Be(Noon - SweepCursorPolicy.Margin, "an idle read moves the cursor to its start, so nothing is read twice");

    [Fact]
    public async Task SweepCursor_Sqlite_CompareAndSetOnTicks()
    {
        using var ctx = _store.Context();
        var cursors = new SweepCursorStore(ctx, new SqliteUniqueViolationTranslator());

        await cursors.AdvanceAsync("tickets:jira", new SweepPosition(Noon), CancellationToken.None);
        await cursors.AdvanceAsync("tickets:jira", new SweepPosition(Noon.AddMinutes(-10)), CancellationToken.None);
        (await cursors.GetAsync("tickets:jira", CancellationToken.None))!.At.Should().Be(Noon, "a stale leader never moves a cursor back");

        await cursors.AdvanceAsync("tickets:jira", new SweepPosition(Noon.AddMinutes(1)), CancellationToken.None);
        (await cursors.GetAsync("tickets:jira", CancellationToken.None))!.At.Should().Be(Noon.AddMinutes(1));
    }

    [Fact]
    public async Task Sweep_RotatingStart_NoSourceStarves()
    {
        var read = new List<string>();
        SweepSource Source(string key) => new(key, (_, _, _, _) =>
        {
            read.Add(key);
            _clock.Now += TimeSpan.FromMinutes(5);
            return Task.FromResult(new ChangedPage([]));
        }, (_, _) => Task.CompletedTask);
        using (var ctx = _store.Context())
            foreach (var key in new[] { "a", "b" })
                await new SweepCursorStore(ctx, new SqliteUniqueViolationTranslator()).AdvanceAsync(key, new SweepPosition(Noon), CancellationToken.None);
        var cycle = new ChangeSweepCycle(_store.ScopeFactory, _clock, NullLogger<ChangeSweepCycle>.Instance);

        await cycle.RunAsync([Source("a"), Source("b")], TimeSpan.FromMinutes(1), CancellationToken.None);
        await cycle.RunAsync([Source("a"), Source("b")], TimeSpan.FromMinutes(1), CancellationToken.None);

        read.Should().Equal("a", "b");
    }
}
