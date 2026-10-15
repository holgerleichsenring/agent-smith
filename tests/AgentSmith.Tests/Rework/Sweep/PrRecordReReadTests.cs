using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.Rework.Records;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>
/// 2026-10-14-dbceg: the periodic re-read nudges a Refresh for stale open records of webhook projects,
/// unread records of every configured project and merged tickets that never landed — one per ticket,
/// oldest check first, capped, so no record holds its place.
/// </summary>
public sealed class PrRecordReReadTests : IDisposable
{
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly Mock<IConfigurationLoader> _loader = new();
    private readonly PrReviewRefreshHarness _h;

    public PrRecordReReadTests()
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ReworkNudgeRequest r, CancellationToken _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        _h = new PrReviewRefreshHarness(services => services.AddSingleton(nudges.Object).AddSingleton(_ => _h!.Wake()));
        Configure(Project("web", "hooks"), Project("web2", "hooks"), Project("poll", "poller", polling: true));
    }

    public void Dispose() => _h.Dispose();

    private DateTimeOffset Now => _h.Clock.GetUtcNow();

    private static ResolvedProject Project(string name, string tracker, bool polling = false) => new()
    {
        Name = name,
        Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.GitHub, Polling = new PollingConfig { Enabled = polling } },
    };

    private void Configure(params ResolvedProject[] projects) =>
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig { Projects = projects.ToDictionary(p => p.Name) });

    private Task<int> CycleAsync() =>
        new PrRecordReReadHostedService(_h.Store.Services, new ServerContext("agentsmith.yml"), _loader.Object, _h.Clock,
            NullLogger<PrRecordReReadHostedService>.Instance).ReReadOnceAsync(CancellationToken.None);

    private static string Url(string project, string number) => $"https://github.com/o/{project}/pull/{number}";

    private async Task SeedAsync(string project, string ticket, string number, string state, TimeSpan checkedAgo)
    {
        await using var db = _h.Store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(Url(project, number)), Url = Url(project, number), Repo = "api", Number = number, Project = project,
            TicketId = ticket, RunId = "run-1", State = state, CheckedAtTicks = (Now - checkedAgo).UtcTicks,
            StateAtTicks = PrReviewStates.IsEnded(state) ? (Now - checkedAgo).UtcTicks : null,
        });
        await db.SaveChangesAsync();
    }

    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(20);

    private IEnumerable<(string, string)> Tickets => _nudged.Select(n => (n.Project, n.TicketId));

    [Fact]
    public async Task PrRecordReRead_OpenRecordUncheckedFifteenMinutes_EnqueuesOneRefreshNudge()
    {
        await SeedAsync("web", "7", "1", PrReviewStates.Open, Stale);

        (await CycleAsync()).Should().Be(1);

        _nudged.Should().ContainSingle().Which.Should().Be(
            new ReworkNudgeRequest("web", "7", ReworkNudgeOrigin.Refresh, Url("web", "1")));
    }

    [Fact]
    public async Task PrRecordReRead_RecentlyCheckedOrEndedRecord_EnqueuesNothing()
    {
        await SeedAsync("web", "1", "1", PrReviewStates.Open, TimeSpan.FromMinutes(10));
        await SeedAsync("web", "2", "2", PrReviewStates.Closed, Stale);
        await SeedAsync("web", "3", "3", PrReviewStates.Unsupported, Stale);
        await SeedAsync("web", "4", "4", PrReviewStates.Merged, TimeSpan.FromMinutes(10));

        (await CycleAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrRecordReRead_SixtyStaleRecords_NudgesOnlyTheFiftyOldest()
    {
        for (var i = 0; i < 60; i++) await SeedAsync("web", $"t{i}", $"{i}", PrReviewStates.Open, Stale + TimeSpan.FromMinutes(i));

        (await CycleAsync()).Should().Be(50);

        _nudged.Select(n => n.TicketId).Should().BeEquivalentTo(Enumerable.Range(10, 50).Select(i => $"t{i}"));
    }

    [Fact]
    public async Task PrRecordReRead_TwoStaleRecordsOfOneTicket_EnqueueOneNudge()
    {
        await SeedAsync("web", "7", "1", PrReviewStates.Open, Stale);
        await SeedAsync("web", "7", "2", PrReviewStates.Unread, Stale);

        (await CycleAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PrRecordReRead_FiftyRecordsWhoseReadFails_DoNotStarveTheRest()
    {
        for (var i = 0; i < 60; i++) await SeedAsync("web", $"t{i}", $"{i}", PrReviewStates.Open, Stale + TimeSpan.FromMinutes(i));
        await CycleAsync();
        foreach (var nudge in _nudged)
        {
            await using var db = _h.Store.Context();
            var record = await db.Set<PrReviewRecord>().AsNoTracking().SingleAsync(r => r.Url == nudge.PrUrl);
            record.State = PrReviewStates.Unread;
            record.CheckedAtTicks = Now.UtcTicks;
            await new PrReviewRecordWriter(db, new SqliteUniqueViolationTranslator()).MarkUnreadAsync(record, Now.UtcTicks, default);
        }
        var first = _nudged.Select(n => n.TicketId).ToList();
        _nudged.Clear();
        _h.Clock.Now += PrRecordReReadHostedService.Interval;

        await CycleAsync();

        _nudged.Select(n => n.TicketId).Should().BeEquivalentTo(Enumerable.Range(0, 10).Select(i => $"t{i}"),
            "a failed read advanced the fifty's check time, so the ten left reach the front");
        first.Should().NotIntersectWith(_nudged.Select(n => n.TicketId));
    }

    [Fact]
    public async Task PrRecordReRead_RecordsOfAnUnconfiguredProject_AreNotSelected()
    {
        await SeedAsync("gone", "1", "1", PrReviewStates.Open, Stale);
        await SeedAsync("gone", "2", "2", PrReviewStates.Unread, Stale);
        await SeedAsync("gone", "3", "3", PrReviewStates.Merged, Stale);

        (await CycleAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrRecordReRead_UnreadRecordUncheckedFifteenMinutes_IsReRead()
    {
        await SeedAsync("web", "1", "1", PrReviewStates.Unread, Stale);
        await SeedAsync("poll", "2", "2", PrReviewStates.Unread, Stale);

        await CycleAsync();

        Tickets.Should().BeEquivalentTo([("web", "1"), ("poll", "2")], "the PR sweep never nudges an unread record");
    }

    [Fact]
    public async Task PrRecordReRead_OpenRecordOfAPollingTracker_IsLeftToThePrSweep()
    {
        await SeedAsync("poll", "1", "1", PrReviewStates.Open, Stale);

        (await CycleAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrRecordReRead_UnlandedMergedTicketOfAPollingTracker_IsStillRecovered()
    {
        await SeedAsync("poll", "1", "1", PrReviewStates.Merged, Stale);

        await CycleAsync();

        Tickets.Should().Equal(("poll", "1"));
    }

    [Fact]
    public async Task PrRecordReRead_MergedTicketWithAnOpenRecord_IsNotInTheRecoveryList()
    {
        await SeedAsync("poll", "1", "1", PrReviewStates.Merged, Stale);
        await SeedAsync("poll", "1", "2", PrReviewStates.Open, Stale);
        await SeedAsync("web", "2", "3", PrReviewStates.Merged, Stale);
        await SeedAsync("web", "2", "4", PrReviewStates.Unsupported, Stale);

        (await CycleAsync()).Should().Be(0, "the polling project's open record is the sweep's; neither ticket ended");
    }

    [Fact]
    public async Task PrRecordReRead_TicketLandedUnderAnotherProjectOfTheSameTracker_IsNotSelected()
    {
        await SeedAsync("web2", "7", "1", PrReviewStates.Merged, Stale);
        await new LandedTicketRepository(_h.Store.ScopeFactory).TryRecordAsync(new TicketLandingFact(
            "hooks", "github-7", "7", "web", TicketLandingKinds.Merged, Now, null, null, Url("web", "9")), default);

        (await CycleAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrRecordReRead_MergedTicketWithoutLanding_EnqueuesARefreshThatRecordsIt()
    {
        Configure(PrReviewRefreshHarness.Project);
        await _h.RunAsync("run-1", PrReviewRefreshHarness.Pr1);
        _h.State(PrReviewRefreshHarness.Pr1, PrReviewStates.Open);
        await _h.RefreshAsync();
        await using (var db = _h.Store.Context())
            await db.Set<PrReviewRecord>().ExecuteUpdateAsync(s => s
                .SetProperty(r => r.State, PrReviewStates.Merged).SetProperty(r => r.CheckedAtTicks, (Now - Stale).UtcTicks));
        _h.Landings().Should().BeEmpty("the merge was written but its landing was lost");
        _h.State(PrReviewRefreshHarness.Pr1, PrReviewStates.Merged);

        (await CycleAsync()).Should().Be(1);
        await _h.Refresh().RefreshAsync(PrReviewRefreshHarness.Project, _nudged[0].TicketId, _nudged[0].PrUrl, default);

        _h.Landings().Should().ContainSingle().Which.Tracker.Should().Be("gh-tracker");
    }
}
