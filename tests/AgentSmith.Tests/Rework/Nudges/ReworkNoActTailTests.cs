using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Nudges;

/// <summary>2026-10-13-8762v: the tail of one nudge — an unfinished attempt waits, a pending act is served before the
/// series, and only no act (none, or one followed up) asks the series to go on.</summary>
public sealed class ReworkNoActTailTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly ResolvedProject Project = new() { Name = "p", Tracker = new TrackerConnection { Type = TrackerType.Jira } };
    private static readonly ClaimedReworkNudge Nudge = new("p", "7", ReworkNudgeOrigin.RunEnd, null, "Ticket", 1, "tok", 0);
    private static readonly PendingReworkAct Act = new(new ReworkAct("alice", Start.AddMinutes(30)));
    private readonly ServerStateStore _store = new(configure: ClosedTicketFollowUps.Register);
    private readonly Mock<IReworkPendingActs> _pending = new();
    private readonly Mock<ISeriesAutoStart> _series = new();
    private readonly Mock<ITicketProviderFactory> _tickets = new();

    public ReworkNoActTailTests()
    {
        _tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(Mock.Of<ITicketProvider>());
        _series.Setup(s => s.ContinueAsync(Project, "7", It.IsAny<CancellationToken>())).ReturnsAsync(ReworkNudgeDisposition.Reschedule);
    }

    public void Dispose() => _store.Dispose();

    private ReworkSpeech Speech() =>
        new(_store.ScopeFactory, _tickets.Object, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);

    private ReworkNoActTail Tail()
    {
        var speech = Speech();
        return new ReworkNoActTail(_pending.Object, ClosedTicketFollowUps.Of(_store.ScopeFactory, _tickets.Object, speech),
            new ReworkWithheld(_store.ScopeFactory, speech), _series.Object);
    }

    private static PreviousAttempt Attempt(bool finished) => new("run-1", finished ? "success" : "running", Start, finished);

    private void Pending(PendingReworkAct? act) =>
        _pending.Setup(p => p.NewestAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>())).ReturnsAsync(act);

    private async Task MarkAsync(Func<ReworkLedgerRepository, Task> mark)
    {
        using var ctx = _store.Context();
        await mark(new ReworkLedgerRepository(ctx, new SqliteUniqueViolationTranslator()));
    }

    [Fact]
    public async Task ReworkNoActTail_UnfinishedAttempt_Reschedules()
    {
        var tail = await Tail().ReadAsync(Project, Nudge, Attempt(finished: false), CancellationToken.None);

        tail.Should().Be(new ReworkTail(ReworkNudgeDisposition.Reschedule));
        _pending.VerifyNoOtherCalls();
        _series.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReworkNoActTail_ActFollowedUpOrWithheld_Finishes()
    {
        Pending(Act);
        await MarkAsync(l => l.TryMarkSpokenAsync("p", "7", ClosedTicketActFollowUp.KeyOf(Act.Act), CancellationToken.None));

        var followedUp = await Tail().ReadAsync(Project, Nudge, Attempt(finished: true), CancellationToken.None);

        followedUp.Act.Should().BeNull("an act already followed up is spent, so the series is asked");
        followedUp.Disposition.Should().Be(ReworkNudgeDisposition.Reschedule, "the series' own answer is the nudge's");

        var withheldAct = new PendingReworkAct(new ReworkAct("bob", Start.AddMinutes(40)));
        Pending(withheldAct);
        await MarkAsync(l => l.RaiseAsync("p", "7", Start.AddMinutes(50), CancellationToken.None));

        var withheld = await Tail().ReadAsync(Project, Nudge, Attempt(finished: true), CancellationToken.None);

        withheld.Should().Be(new ReworkTail(ReworkNudgeDisposition.Finish));
        _series.Verify(s => s.ContinueAsync(Project, "7", It.IsAny<CancellationToken>()), Times.Once,
            "a withheld act is said once and the nudge ends; only the spent act asked the series");
    }

    [Fact]
    public async Task ReworkNudgeHandler_PendingAct_IsServedBeforeTheSeries()
    {
        Pending(Act);
        var rework = new Mock<IReworkEntry>();
        rework.Setup(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("run-2"));
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>())).ReturnsAsync(Attempt(finished: true));
        var speech = Speech();
        var handler = ReworkHandlers.Of(Mock.Of<IActiveRunLease>(), attempts.Object, _pending.Object, rework.Object, speech,
            new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), _tickets.Object,
                new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
                Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance),
            new ReworkWithheld(_store.ScopeFactory, speech), Mock.Of<IRunCheckpointStore>(),
            ClosedTicketFollowUps.Of(_store.ScopeFactory, _tickets.Object, speech), _series.Object);

        (await handler.HandleAsync(Project, Nudge, CancellationToken.None)).Should().Be(ReworkNudgeDisposition.Finish);

        rework.Verify(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()), Times.Once);
        _series.VerifyNoOtherCalls();
    }
}
