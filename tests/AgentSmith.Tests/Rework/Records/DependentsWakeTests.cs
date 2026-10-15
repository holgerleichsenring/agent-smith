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
/// 2026-10-14-dbceh: the landing of a required ticket wakes each ticket waiting for it with one RequirementsMet
/// nudge — only on the replica whose insert won, and before the announcement, so a failed Redis publish never
/// loses the wake.
/// </summary>
public sealed class DependentsWakeTests : IDisposable
{
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly PrReviewRefreshHarness _h;

    public DependentsWakeTests() => _h = new PrReviewRefreshHarness(services => services.AddSingleton(Nudges(_nudged)));

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task DependentsWake_Landing_EnqueuesOneRequirementsMetNudgePerDependent()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await EdgeAsync(_h.Store, "9", "7");
        await EdgeAsync(_h.Store, "9", "6");
        await EdgeAsync(_h.Store, "10", "6");
        await SeedMergedAsync(_h.Store);

        (await RecordAsync()).Should().BeTrue();

        _nudged.Select(n => (n.Project, n.TicketId, n.Origin)).Should().BeEquivalentTo(
            [("p", "8", ReworkNudgeOrigin.RequirementsMet), ("p", "9", ReworkNudgeOrigin.RequirementsMet)],
            "the tickets waiting for 7, once each; 10 waits for 6 only");
    }

    [Fact]
    public async Task TicketLandingRecorder_LostInsert_WakesNothing()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await SeedMergedAsync(_h.Store);
        (await RecordAsync()).Should().BeTrue();
        _nudged.Clear();

        (await RecordAsync()).Should().BeFalse();

        _nudged.Should().BeEmpty("a replica whose insert lost decides nothing");
    }

    [Fact]
    public async Task TicketLandingRecorder_EventPublishThrows_DependentsStillWoken()
    {
        await EdgeAsync(_h.Store, "8", "7");
        await SeedMergedAsync(_h.Store);
        _h.Events.Setup(e => e.PublishAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        (await RecordAsync()).Should().BeTrue();

        _nudged.Should().ContainSingle().Which.TicketId.Should().Be("8");
    }

    [Fact]
    public async Task TicketLandingRecorder_TwoRacingReplicas_WakeTheDependentsOnce()
    {
        var fired = false;
        ServerStateStore? store = null;
        List<ReworkNudgeRequest> nudgedA = [], nudgedB = [];
        TicketLandingRecorder Replica(List<ReworkNudgeRequest> nudged) =>
            new(store!.ScopeFactory, new LandedTicketRepository(store.ScopeFactory), WakeOver(store, Nudges(nudged)),
                Mock.Of<ISystemEventPublisher>(), Mock.Of<ITicketActivityNudge>(), _h.Clock, NullLogger<TicketLandingRecorder>.Instance);
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

        nudgedB.Should().ContainSingle().Which.Origin.Should().Be(ReworkNudgeOrigin.RequirementsMet);
        nudgedA.Should().BeEmpty();
    }

    private Task<bool> RecordAsync() => _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None);

    private static IReworkNudges Nudges(List<ReworkNudgeRequest> into)
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ReworkNudgeRequest r, CancellationToken _) => into.Add(r)).Returns(Task.CompletedTask);
        return nudges.Object;
    }

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
