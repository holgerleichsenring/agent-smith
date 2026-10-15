using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-14-dbceg: a ticket whose pull requests ended with one merged is recorded as landed once; only
/// the replica whose insert won announces it, and a failed announcement never loses the row.
/// </summary>
public sealed class TicketLandingRecorderTests : IDisposable
{
    private const string Pr3 = "https://github.com/o/api/pull/3";
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static async Task SeedAsync(ServerStateStore store, string url, string state, int hours = 1)
    {
        await using var db = store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = url.Split('/')[^1], Project = "p", TicketId = "7",
            RunId = "run-1", State = state, StateAtTicks = T.AddHours(hours).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
        });
        await db.SaveChangesAsync();
    }

    private Task<bool> RecordAsync() => _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None);

    [Fact]
    public async Task TicketLandingRecorder_EveryRecordEndedOneMerged_RecordsOneMergedLanding()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Merged);
        await SeedAsync(_h.Store, Pr2, PrReviewStates.Closed);

        (await RecordAsync()).Should().BeTrue();

        _h.Landings().Should().ContainSingle().Which.Should().Match<LandedTicket>(l =>
            l.Tracker == "gh-tracker" && l.TicketKey == "github-7" && l.TicketId == "7" && l.Project == "p"
            && l.Kind == TicketLandingKinds.Merged && l.By == null && l.Note == null && l.PrUrl == Pr1);
        _h.Events.Verify(e => e.PublishAsync(It.Is<TicketLandedEvent>(x =>
            x.Source == TicketLandingRecorder.Source && x.Project == "p" && x.TicketKey == "github-7" && x.TicketId == "7"
            && x.Kind == TicketLandingKinds.Merged && x.By == null), It.IsAny<CancellationToken>()), Times.Once);
        _h.Activity.Verify(a => a.ChangedAsync("p", "github-7", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TicketLandingRecorder_OneRecordStillOpen_RecordsNothing()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Merged);
        await SeedAsync(_h.Store, Pr2, PrReviewStates.Open);

        (await RecordAsync()).Should().BeFalse();

        _h.Landings().Should().BeEmpty();
        _h.Events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TicketLandingRecorder_EveryRecordClosedUnmerged_RecordsNothing()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Closed);
        await SeedAsync(_h.Store, Pr2, PrReviewStates.Closed);

        (await RecordAsync()).Should().BeFalse();

        _h.Landings().Should().BeEmpty();
    }

    [Fact]
    public async Task TicketLandingRecorder_LandedAt_IsTheNewestHostStateAt()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Merged, hours: 2);
        await SeedAsync(_h.Store, Pr2, PrReviewStates.Merged, hours: 1);
        await SeedAsync(_h.Store, Pr3, PrReviewStates.Closed, hours: 5);

        await RecordAsync();

        _h.Landings().Single().Should().Match<LandedTicket>(l => l.LandedAt == T.AddHours(5) && l.PrUrl == Pr1,
            "the landing time is the last pull request's end; the URL is the newest merged one");
    }

    [Fact]
    public async Task TicketLandingRecorder_SecondRefreshOfALandedTicket_InsertsAndPublishesNothing()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Merged);

        (await RecordAsync()).Should().BeTrue();
        (await RecordAsync()).Should().BeFalse();

        _h.Landings().Should().ContainSingle();
        _h.Events.Verify(e => e.PublishAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _h.Activity.Verify(a => a.ChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TicketLandingRecorder_TwoRacingReplicas_OneInsertOneEventOneNudge()
    {
        var fired = false;
        ServerStateStore? store = null;
        Mock<ISystemEventPublisher> eventsA = new(), eventsB = new();
        Mock<ITicketActivityNudge> activityA = new(), activityB = new();
        TicketLandingRecorder Replica(Mock<ISystemEventPublisher> events, Mock<ITicketActivityNudge> activity) =>
            new(store!.ScopeFactory, new LandedTicketRepository(store.ScopeFactory), WakeOver(store, Mock.Of<IReworkNudges>()),
                events.Object, activity.Object, _h.Clock, NullLogger<TicketLandingRecorder>.Instance);
        using var racing = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            (await Replica(eventsB, activityB).RecordMergedAsync(Project, "7", default)).Should().BeTrue();
        }));
        store = racing;
        await SeedAsync(racing, Pr1, PrReviewStates.Merged);
        fired = false;

        (await Replica(eventsA, activityA).RecordMergedAsync(Project, "7", default)).Should().BeFalse("the other replica's insert won");

        fired.Should().BeTrue();
        await using var db = racing.Context();
        (await db.Set<LandedTicket>().CountAsync()).Should().Be(1);
        eventsB.Verify(e => e.PublishAsync(It.IsAny<TicketLandedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        activityB.Verify(a => a.ChangedAsync("p", "github-7", It.IsAny<CancellationToken>()), Times.Once);
        eventsA.VerifyNoOtherCalls();
        activityA.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TicketLandingRecorder_PersonPushedCommitsThenMerged_CountsAsMerged()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _h.Threads([new PrReviewThread("a.cs", 3, true, [Note("C1", "alice", "pushed the fix myself and merged")]) { HostId = "T1" }]);

        await _h.RefreshAsync();

        _h.Landings().Should().ContainSingle().Which.Kind.Should().Be(TicketLandingKinds.Merged,
            "the host's merged state is the fact, whoever pushed and merged");
    }

    [Fact]
    public async Task TicketLandingRecorder_RedisPublishThrows_RowStoredAndCallReturnsInserted()
    {
        await SeedAsync(_h.Store, Pr1, PrReviewStates.Merged);
        _h.Events.Setup(e => e.PublishAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));
        _h.Activity.Setup(a => a.ChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        (await RecordAsync()).Should().BeTrue();

        _h.Landings().Should().ContainSingle();
        _h.Activity.Verify(a => a.ChangedAsync("p", "github-7", It.IsAny<CancellationToken>()), Times.Once,
            "a failed event publish never skips the page nudge");
    }

    [Fact]
    public async Task PrReviewRecordRefresh_RecordTurnsMerged_LandingRecordedInThatRefresh()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Open);
        await _h.RefreshAsync();
        _h.Landings().Should().BeEmpty();

        _h.State(Pr1, PrReviewStates.Merged);
        _h.Clock.Now += TimeSpan.FromMinutes(1);
        await _h.RefreshAsync(Pr1);

        _h.Landings().Should().ContainSingle().Which.PrUrl.Should().Be(Pr1);
    }

    [Fact]
    public async Task PrReviewRecordRefresh_PagesNudgeThrowsWhenTheMergeIsSeen_LandingStillRecorded()
    {
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _h.Activity.Setup(a => a.ChangedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var refresh = () => _h.RefreshAsync();

        await refresh.Should().ThrowAsync<InvalidOperationException>("the refresh's own page nudge still fails loudly");
        _h.Landings().Should().ContainSingle("the landing is written before the pages' nudge");
    }
}
