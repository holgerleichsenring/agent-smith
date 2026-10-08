using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
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

/// <summary>2026-10-08-0781: one nudge through the worker's decision — database first, host acts second.</summary>
public sealed class ReworkNudgeHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkPendingActs> _pending = new();
    private readonly Mock<IReworkEntry> _rework = new();
    private readonly Mock<ITicketProvider> _ticket = new();
    private static readonly ResolvedProject Project = new() { Name = "p", Tracker = new TrackerConnection { Type = TrackerType.Jira } };
    private static readonly ClaimedReworkNudge Nudge = new("p", "7", ReworkNudgeOrigin.Ticket, null, "Ticket", 1, "tok", 0);
    private static readonly PendingReworkAct Act = new(new ReworkAct("alice", Start.AddMinutes(30)));

    public void Dispose() => _store.Dispose();

    private ReworkNudgeHandler Handler()
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(_ticket.Object);
        var speech = new ReworkSpeech(_store.ScopeFactory, tickets.Object, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), tickets.Object,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        return new ReworkNudgeHandler(_leases.Object, _attempts.Object, _pending.Object, _rework.Object, speech, fallback,
            new ReworkWithheld(_store.ScopeFactory, speech));
    }

    private void Attempt(string status, bool finished, DateTimeOffset? readAt = null) =>
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", status, Start, finished) { ActsReadAt = readAt });

    private void Pending(PendingReworkAct? act) =>
        _pending.Setup(p => p.NewestAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>())).ReturnsAsync(act);

    private void Outcome(ReworkOutcome outcome) =>
        _rework.Setup(r => r.EnterAsync(Project, "7", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>())).ReturnsAsync(outcome);

    private Task<ReworkNudgeDisposition> Handle() => Handler().HandleAsync(Project, Nudge, CancellationToken.None);

    [Fact]
    public async Task Worker_ReleaseBeforeProjection_ReschedulesThenServes()
    {
        Attempt("running", finished: false);
        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule, "the row is not terminal yet");
        _pending.VerifyNoOtherCalls();

        Attempt("success", finished: true);
        Pending(Act);
        Outcome(ReworkOutcome.Started("run-2"));
        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);
        _rework.Verify(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_ParkedRun_ServedAfterResumeEnds()
    {
        Attempt("waiting_for_input", finished: false);
        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule);
        _rework.VerifyNoOtherCalls();

        Attempt("success", finished: true);
        Pending(Act);
        Outcome(ReworkOutcome.Started("run-2"));
        await Handle();
        _rework.Verify(r => r.EnterAsync(Project, "7", Act.Act, "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Worker_ActDuringRead_ServedAgainNotLost()
    {
        var read = Start.AddMinutes(1);
        var attempt = new PreviousAttempt("run-1", "success", Start, true) { ActsReadAt = read };

        attempt.Precedes(read.AddSeconds(-3)).Should().BeTrue("an act during the read is served again, never lost");
        attempt.Precedes(read.AddSeconds(10)).Should().BeTrue();
        attempt.Precedes(read.AddSeconds(-30)).Should().BeFalse("the run read it");
    }

    [Fact]
    public async Task Worker_OperatorCancel_ActsBeforeRequestNotServed()
    {
        Attempt("cancelled", finished: true);
        Pending(Act);
        using (var ctx = _store.Context())
            await new ReworkLedgerRepository(ctx, new SqliteUniqueViolationTranslator()).RaiseAsync("p", "7", Act.Act.At.AddMinutes(1), CancellationToken.None);

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);
        await Handle();

        _rework.VerifyNoOtherCalls();
        _ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.Is<string>(s => s.Contains("not picked up")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_LiveRunAct_SpokenOnce()
    {
        Attempt("running", finished: false, readAt: Start.AddMinutes(1));
        _leases.Setup(l => l.GetByTicketAsync("p", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("7"), "run-1", null));
        Pending(Act);

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish, "the live run's end nudges again");
        await Handle();

        _rework.VerifyNoOtherCalls();
        _ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.Is<string>(s => s.Contains("picked up when it finishes")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_LiveRunNotYetRead_SaysNothing()
    {
        Attempt("running", finished: false);
        _leases.Setup(l => l.GetByTicketAsync("p", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("7"), "run-1", null));

        await Handle();

        _pending.VerifyNoOtherCalls();
        _ticket.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Worker_NoRunStarted_Rescheduled()
    {
        Attempt("success", finished: true);
        Pending(Act);
        Outcome(ReworkOutcome.Started(null));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule);
    }

    [Fact]
    public async Task Worker_QueuedCodeRun_Dropped()
    {
        Attempt("success", finished: true);
        _attempts.Setup(a => a.HasQueuedAsync("p", "7", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);
        _pending.VerifyNoOtherCalls();
    }
}
