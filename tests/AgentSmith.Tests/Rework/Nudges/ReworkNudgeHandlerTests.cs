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
    private readonly ServerStateStore _store = new(configure: ClosedTicketFollowUps.Register);
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkPendingActs> _pending = new();
    private readonly Mock<IReworkEntry> _rework = new();
    private readonly Mock<ITicketProvider> _ticket = new();
    private readonly Mock<IRunCheckpointStore> _checkpoints = new();
    private static readonly ResolvedProject Project = new() { Name = "p", Tracker = new TrackerConnection { Type = TrackerType.Jira } };
    private static readonly ClaimedReworkNudge Nudge = new("p", "7", ReworkNudgeOrigin.Ticket, null, "Ticket", 1, "tok", 0);
    private static readonly PendingReworkAct Act = new(new ReworkAct("alice", Start.AddMinutes(30)));

    public void Dispose() => _store.Dispose();

    private ReworkNudgeHandler Handler() => Handler(_rework.Object);

    private ReworkNudgeHandler Handler(IReworkEntry rework)
    {
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(_ticket.Object);
        var speech = new ReworkSpeech(_store.ScopeFactory, tickets.Object, Mock.Of<ISourceProviderFactory>(), NullLogger<ReworkSpeech>.Instance);
        var fallback = new ReworkFallbackSpawn(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"), tickets.Object,
            new PolledTicketEnvelope(ApprovedRecordProbes.None()), Mock.Of<IEnvelopeProjectResolver>(),
            Mock.Of<ISpawnPipelineRunsUseCase>(), NullLogger<ReworkFallbackSpawn>.Instance);
        return ReworkHandlers.Of(_leases.Object, _attempts.Object, _pending.Object, rework, speech, fallback,
            new ReworkWithheld(_store.ScopeFactory, speech), _checkpoints.Object, ClosedTicketFollowUps.Of(_store.ScopeFactory, tickets.Object, speech));
    }

    private void Attempt(string status, bool finished, DateTimeOffset? readAt = null) =>
        _attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", status, Start, finished) { ActsReadAt = readAt });

    private void Pending(PendingReworkAct? act, PendingReworkAct? handbackAnswer = null)
    {
        _pending.Setup(p => p.NewestAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>())).ReturnsAsync(act);
        _pending.Setup(p => p.NewestHandbackAnswerAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handbackAnswer ?? (act?.Act.Channel is ReworkChannel.PullRequest or ReworkChannel.Operator ? act : null));
    }

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

    private static readonly PendingReworkAct PrAct = new(new ReworkAct("alice", Start.AddMinutes(30), ReworkChannel.PullRequest),
        new RepoConnection { Name = "app", Type = RepoType.GitHub }, "https://github.com/o/app/pull/4");

    [Fact]
    public async Task ReworkNudgeHandler_HandbackWaiting_PullRequestAct_ClosesRowWithoutWatermarkAndStarts()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(PrAct);
        var parks = new Mock<IReworkParkCheck>();
        parks.Setup(p => p.CheckHandbackAsync(Project, "7", It.IsAny<CancellationToken>())).ReturnsAsync(new ReworkPark(true, "To Do"));
        parks.Setup(p => p.MoveAsync(Project, "7", "To Do", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var closer = new Mock<IWaitingRunCloser>();
        var launch = new Mock<IReworkLaunch>();
        launch.Setup(l => l.LaunchAsync("p", "7", "code", It.IsAny<CancellationToken>())).ReturnsAsync(ReworkOutcome.Started("run-2"));
        var entry = new ReworkEntry(_leases.Object, _attempts.Object, parks.Object, Mock.Of<ITicketReopener>(), launch.Object,
            closer.Object, NullLogger<ReworkEntry>.Instance);

        (await Handler(entry).HandleAsync(Project, Nudge, CancellationToken.None)).Should().Be(ReworkNudgeDisposition.Finish);

        closer.Verify(c => c.CloseSupersededAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        launch.Verify(l => l.LaunchAsync("p", "7", "code", It.IsAny<CancellationToken>()), Times.Once);
        parks.Verify(p => p.CheckAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        using var ctx = _store.Context();
        (await new ReworkLedgerRepository(ctx, new SqliteUniqueViolationTranslator()).WatermarkAsync("p", "7", CancellationToken.None))
            .Should().BeNull("a superseded hand-back is no operator stop");
    }

    [Fact]
    public async Task ReworkNudgeHandler_CheckpointedQuestion_PullRequestAct_KeepsWaiting()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(PrAct);
        _checkpoints.Setup(c => c.GetByRunIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunCheckpointRecord("run-1", "p", "7", null, "code", "job", "q", "{}", "[]", "{}", 1, Start, Start.AddDays(1), null));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule, "the question's answer brings the run back");

        _rework.Verify(r => r.AnswerHandbackAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<PreviousAttempt>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        AskedOnlyWhetherClosed();
        _rework.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReworkNudgeHandler_HandbackWaiting_TicketAct_KeepsWaiting()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(Act);

        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule, "only a pull-request act answers a hand-back");

        AskedOnlyWhetherClosed();
        _rework.VerifyNoOtherCalls();
    }

    private static readonly PendingReworkAct OperatorApproval = new(new ReworkAct("owner", Start.AddMinutes(20), ReworkChannel.Operator)
    {
        Source = new AttemptSource(AttemptSourceKinds.Conversation, Id: "s-1"),
    });

    private void Answers(ReworkOutcome outcome) =>
        _rework.Setup(r => r.AnswerHandbackAsync(Project, "7", It.IsAny<PreviousAttempt>(), "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

    [Fact]
    public async Task NudgeHandler_HandbackPark_OperatorActAnswersThroughAnswerHandback()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(OperatorApproval);
        Answers(ReworkOutcome.Started("run-2"));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        _rework.Verify(r => r.AnswerHandbackAsync(Project, "7", It.Is<PreviousAttempt>(a => a.RunId == "run-1"), "code",
            It.IsAny<CancellationToken>()), Times.Once);
        _rework.Verify(r => r.EnterAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<ReworkAct>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NudgeHandler_HandbackPark_LaterKeywordComment_OperatorActStillAnswers()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(Act, handbackAnswer: OperatorApproval); // the keyword comment (minute 30) is newer than the approval (20)
        Answers(ReworkOutcome.Started("run-2"));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish, "a later keyword comment never hides the approval");

        _rework.Verify(r => r.AnswerHandbackAsync(Project, "7", It.IsAny<PreviousAttempt>(), "code",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NudgeHandler_CheckpointedQuestion_OperatorActWaits()
    {
        Attempt("waiting_for_input", finished: false, readAt: Start.AddMinutes(1));
        Pending(OperatorApproval);
        _checkpoints.Setup(c => c.GetByRunIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunCheckpointRecord("run-1", "p", "7", null, "code", "job", "q", "{}", "[]", "{}", 1, Start, Start.AddDays(1), null));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Reschedule, "the question's answer brings the run back first");

        _rework.Verify(r => r.AnswerHandbackAsync(It.IsAny<ResolvedProject>(), It.IsAny<string>(), It.IsAny<PreviousAttempt>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        AskedOnlyWhetherClosed();
        _rework.VerifyNoOtherCalls();
    }

    // 2026-10-14-dbcei: the refusal names where the act came from — here the ticket's conversation.
    [Fact]
    public async Task ReworkNudgeHandler_RefusedConversationApproval_PostsTheConversationSentence()
    {
        Attempt("success", finished: true);
        Pending(OperatorApproval);
        Outcome(ReworkOutcome.Refused("the capacity queue is full"));

        (await Handle()).Should().Be(ReworkNudgeDisposition.Finish);

        _ticket.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(),
            "Agent Smith — the request in this ticket's conversation did not start a rework: the capacity queue is full.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2026-10-13-9a59t: a waiting attempt's act asks whether the ticket was closed; an open one keeps waiting.
    private void AskedOnlyWhetherClosed() =>
        _rework.Verify(r => r.SupersedeIfClosedAsync(Project, "7", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()), Times.Once);
}
