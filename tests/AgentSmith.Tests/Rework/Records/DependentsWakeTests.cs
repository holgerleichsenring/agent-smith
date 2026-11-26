using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-10-14-dbceh: the landing of a required ticket wakes each ticket waiting for it — 2026-11-25-96aea: a
/// prerequisite wake on the series that holds it, never a start — only on the replica whose insert won, and before the announcement, so a failed Redis publish never
/// loses the wake.
/// </summary>
public sealed class DependentsWakeTests : IDisposable
{
    private readonly RecordingSeriesWakes _nudged = new();
    private readonly PrReviewRefreshHarness _h;

    public DependentsWakeTests() => _h = new PrReviewRefreshHarness(services => services.AddSingleton<AgentSmith.Contracts.Series.ISeriesWakes>(_nudged));

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task DependentsWake_Landing_RecordsOnePrerequisiteWakePerDependent()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await EdgeAsync(_h.Store, "9", "7");
        await EdgeAsync(_h.Store, "9", "6");
        await EdgeAsync(_h.Store, "10", "6");
        await SeedMergedAsync(_h.Store);

        (await RecordAsync()).Should().BeTrue();

        _nudged.Woken.Select(n => (n.Project, n.TicketId, n.Wake.Kind)).Should().BeEquivalentTo(
            [("p", "8", SeriesWakeKinds.Prerequisite), ("p", "9", SeriesWakeKinds.Prerequisite)],
            "the tickets waiting for 7, once each; 10 waits for 6 only");
    }

    [Fact]
    public async Task TicketLandingRecorder_LostInsert_WakesNothing()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await SeedMergedAsync(_h.Store);
        (await RecordAsync()).Should().BeTrue();
        _nudged.Woken.Clear();

        (await RecordAsync()).Should().BeFalse();

        _nudged.Woken.Should().BeEmpty("a replica whose insert lost decides nothing");
    }

    [Fact]
    public async Task TicketLandingRecorder_EventPublishThrows_DependentsStillWoken()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await SeedMergedAsync(_h.Store);
        _h.Events.Setup(e => e.PublishAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        (await RecordAsync()).Should().BeTrue();

        _nudged.Woken.Should().ContainSingle().Which.TicketId.Should().Be("8");
    }

    [Fact]
    public async Task TicketLandingRecorder_TwoRacingReplicas_WakeTheDependentsOnce()
    {
        var fired = false;
        ServerStateStore? store = null;
        RecordingSeriesWakes nudgedA = new(), nudgedB = new();
        TicketLandingRecorder Replica(RecordingSeriesWakes nudged) =>
            new(store!.ScopeFactory, new LandedTicketRepository(store.ScopeFactory), WakeOver(store, nudged),
                Mock.Of<ISystemEventPublisher>(), Mock.Of<ITicketActivityNudge>(), [], StackedOver(store), _h.Clock,
                NullLogger<TicketLandingRecorder>.Instance);
        using var racing = new ServerStateStore(unitOfWork: db => new RacingUnitOfWork(db, async () =>
        {
            if (fired) return;
            fired = true;
            (await Replica(nudgedB).RecordMergedAsync(Project, "7", default)).Should().BeTrue();
        }));
        store = racing;
        await EdgeAsync(racing, "8", "7");
        await SeedMergedAsync(racing);
        fired = false;

        (await Replica(nudgedA).RecordMergedAsync(Project, "7", default)).Should().BeFalse("the other replica's insert won");

        nudgedB.Woken.Should().ContainSingle().Which.Wake.Kind.Should().Be(SeriesWakeKinds.Prerequisite);
        nudgedA.Woken.Should().BeEmpty();
    }

    private Task<bool> RecordAsync() => _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None);

    private static async Task EdgeAsync(ServerStateStore store, string dependent, string required)
    {
        await using var db = store.Context();
        db.Add(new TicketRequirement
        {
            Tracker = "gh-tracker", TicketKey = $"github-{dependent}", TicketId = dependent, Project = "p",
            RequiredTicketKey = $"github-{required}", RequiredTicketId = required, StartsWhenMet = true, FiledInConversation = "s-1",
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedMergedAsync(ServerStateStore store)
    {
        await using var db = store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(Pr1), Url = Pr1, Repo = "api", Number = "1", Project = "p", TicketId = "7", RunId = "run-1",
            State = PrReviewStates.Merged, StateAtTicks = T.UtcTicks, CheckedAtTicks = T.UtcTicks,
        });
        await db.SaveChangesAsync();
    }
}
