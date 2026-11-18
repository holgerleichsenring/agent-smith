using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147e: per-service unit tests for IPipelineErrorHandler. Covers the
/// failure policy: HTML-formatted ticket comment, WIP-persist read-only guard,
/// lifecycle.MarkFailed, and the "working" status posting.
/// </summary>
public sealed class PipelineErrorHandlerTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<IAsyncPipelineLifecycle> _lifecycleMock = new();
    private readonly InMemoryUnmovedTicketStore _unmovedTickets = new();
    private readonly PipelineErrorHandler _sut;

    public PipelineErrorHandlerTests()
    {
        _sut = new PipelineErrorHandler(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            new FailureTicketComment(),
            new UnmovedTicketReport(_unmovedTickets, NullLogger<UnmovedTicketReport>.Instance),
            NullLogger<PipelineErrorHandler>.Instance);
    }

    // 2026-09-18-c1a7: a finalize that did not move the ticket leaves the record the claim
    // service reads; one that moved it leaves nothing, so the next trigger is claimed normally.
    [Fact]
    public async Task HandleStepFailureAsync_FinalizeReportsTheTicketUnmoved_RecordsIt()
    {
        await FailWithFinalizeAsync(TicketFinalizeResult.NotExpressible("Blocked"));

        var standing = await _unmovedTickets.FindStandingAsync(
            "p1", "99", "tracker-a", CancellationToken.None);
        standing.Should().NotBeNull();
        standing!.ConfiguredStatus.Should().Be("Blocked");
        standing.Outcome.Should().Be(TicketFinalizeOutcome.StatusNotExpressible);
    }

    [Fact]
    public async Task HandleStepFailureAsync_FinalizeMovedTheTicket_RecordsNothing()
    {
        await FailWithFinalizeAsync(TicketFinalizeResult.Moved());

        (await _unmovedTickets.FindStandingAsync("p1", "99", "tracker-a", CancellationToken.None))
            .Should().BeNull();
    }

    // A later finalize that DID move the ticket clears the record an earlier one left, so a
    // standing refusal never outlives the configuration that caused it.
    [Fact]
    public async Task HandleStepFailureAsync_FinalizeMovedTheTicket_ClearsAnEarlierRecord()
    {
        await FailWithFinalizeAsync(TicketFinalizeResult.NotExpressible("Blocked"));

        await FailWithFinalizeAsync(TicketFinalizeResult.Moved());

        (await _unmovedTickets.FindStandingAsync("p1", "99", "tracker-a", CancellationToken.None))
            .Should().BeNull();
    }

    // 2026-11-18-d6d2a: the claim's request reaches the run through the Redis job queue, which hands every context
    // value back as a JsonElement; the failure path reads failed_status from the project, so it still moves.
    [Fact]
    public async Task HandleStepFailureAsync_RequestContextThroughTheJobQueue_MovesToTheProjectsFailedStatus()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var queued = System.Text.Json.JsonSerializer.Deserialize<PipelineRequest>(System.Text.Json.JsonSerializer.Serialize(
            new PipelineRequest("p1", "code", Context: new() { [ContextKeys.DoneStatus] = "Resolved" })))!;
        var context = new PipelineContext();
        foreach (var (key, value) in queued.Context!) context.Set(key, value);
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        await _sut.HandleStepFailureAsync([], FailedStatusProject.With("Failed", "p1", "tracker-a"),
            context, _lifecycleMock.Object, CommandResult.Fail("criteria unmet"), CancellationToken.None);

        context.TryGet<string>(ContextKeys.DoneStatus, out _).Should().BeFalse("the premise: queued values are JsonElements");
        ticketProvider.Verify(t => t.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), "Failed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_TrackerRejectedTheMove_SaysSoOnTheTicket()
    {
        var ticketProvider = await FailWithFinalizeAsync(TicketFinalizeResult.Rejected("Blocked", "RuleValidationException"));

        ticketProvider.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.Is<string>(c =>
            c.Contains("Agent Smith — Failed") && c.Contains("failed_status 'Blocked' could not be set: TrackerRejectedTheStatus")
            && c.Contains("Retry")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_FinalizeThrows_SaysSoNamingTheExceptionType()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider.Setup(t => t.FinalizeAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("https://internal.example/secret-path 503"));
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        await _sut.HandleStepFailureAsync([], FailedStatusProject.With("Blocked"), context, _lifecycleMock.Object,
            CommandResult.Fail("boom"), CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.Is<string>(c =>
            c.Contains("could not be set: HttpRequestException") && !c.Contains("internal.example")), It.IsAny<CancellationToken>()), Times.Once);
    }

    // NoTransition and NotExpressible: the tracker took the comment and kept its status; the unmoved record holds it.
    [Fact]
    public async Task HandleStepFailureAsync_StatusNotExpressible_PostsNoSecondComment()
    {
        var ticketProvider = await FailWithFinalizeAsync(TicketFinalizeResult.NotExpressible("Blocked"));

        ticketProvider.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<Mock<ITicketProvider>> FailWithFinalizeAsync(TicketFinalizeResult finalize)
    {
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider.Setup(t => t.FinalizeAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalize);
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(),
            FailedStatusProject.With("Blocked", "p1", "tracker-a"),
            context, _lifecycleMock.Object, CommandResult.Fail("boom"), CancellationToken.None);
        return ticketProvider;
    }

    [Fact]
    public async Task HandleStepFailureAsync_PostsHtmlFormattedComment_AndMarksLifecycleFailed()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProvider.Object);

        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        var failure = CommandResult.Fail("boom") with
        {
            FailedStep = 2,
            TotalSteps = 3,
            StepName = "Test"
        };

        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(),
            FailedStatusProject.With("Blocked"),
            context,
            _lifecycleMock.Object,
            failure,
            CancellationToken.None);

        // p0261: a failed run TERMINALIZES the native status (comment + status move in
        // one step) — not a comment-only UpdateStatus. The status comes from failed_status.
        ticketProvider.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "99"),
            It.Is<string>(s =>
                s.Contains("<b>Agent Smith — Failed</b>")
                && s.Contains("<b>Step:</b> Test (2/3)")
                && s.Contains("<b>Error:</b> boom")),
            "Blocked",
            It.IsAny<CancellationToken>()), Times.Once);
        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_WorkPersistedByTheTail_NamesTheDraftPrInTheComment()
    {
        // 2026-09-07-f420: the finalizer tail's CommitAndPR published the draft PR before
        // this path runs; the failure comment is the ticket's one comment, so it says
        // where the partial work went.
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        context.Set<IReadOnlyList<AgentSmith.Application.Models.OpenedPullRequest>>(
            ContextKeys.OpenedPullRequests,
            [new AgentSmith.Application.Models.OpenedPullRequest(
                "primary", "https://stub.test/pulls/7", AgentSmith.Application.Models.OpenStatus.Opened)]);

        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(), FailedStatusProject.With("Blocked"), context, _lifecycleMock.Object,
            CommandResult.Fail("boom"), CancellationToken.None);

        ticketProvider.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(),
            It.Is<string>(c => c.Contains("Agent Smith — Failed") && c.Contains("https://stub.test/pulls/7")
                && !c.Contains("Completed")),
            "Blocked", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_NoFailedStatus_CommentsAndLeavesTheStatus()
    {
        // 2026-11-17-166aa: failed_status unset → no done_status fallback; a failure never looks done.
        // The comment goes out comment-only and the ticket keeps its status.
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        context.Set(ContextKeys.DoneStatus, "Resolved");

        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(), new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("boom"), CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.Is<string>(c => c.Contains("Agent Smith — Failed")), It.IsAny<CancellationToken>()), Times.Once);
        ticketProvider.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleFatalFailureAsync_NoFailedStatus_CommentsAndLeavesTheStatus()
    {
        // 2026-11-17-166aa: the thrown-exception path goes the same way as a step failure.
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));
        context.Set(ContextKeys.DoneStatus, "Resolved");

        await _sut.HandleFatalFailureAsync(
            new ResolvedProject(), context, new InvalidOperationException("spawn quota"), CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        ticketProvider.Verify(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleStepFailureAsync_FinalizeThrows_Swallowed_AndStillMarksFailed()
    {
        // p0261: a ticket-write failure (e.g. a deleted ticket) must not fail the run or
        // invalidate a produced PR — the terminalization is best-effort.
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider.Setup(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ticket deleted (404)"));
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        var act = async () => await _sut.HandleStepFailureAsync(
            Array.Empty<string>(), new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("boom"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task FinalizeFailedTicketAsync_MovesTicketToFailedStatus_WithTheGivenMessage()
    {
        // p0381: a failure dressed as a cancel (sandbox-vanished / internal timeout)
        // terminalizes the ticket to failed_status like every other failure.
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        await _sut.FinalizeFailedTicketAsync(
            FailedStatusProject.With("Blocked"), context, "the sandbox vanished", CancellationToken.None);

        ticketProvider.Verify(t => t.FinalizeAsync(
            It.Is<TicketId>(id => id.Value == "99"),
            It.Is<string>(m => m.Contains("the sandbox vanished")),
            "Blocked",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FinalizeFailedTicketAsync_FinalizeThrows_Swallowed_FailSoft()
    {
        // p0381: best-effort — a ticket-write failure during terminalization must
        // never propagate (the run result is already decided).
        var ticketProvider = new Mock<ITicketProvider>();
        ticketProvider.Setup(t => t.FinalizeAsync(
            It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ticket deleted (404)"));
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("99"));

        var act = async () => await _sut.FinalizeFailedTicketAsync(
            new ResolvedProject(), context, "boom", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleStepFailureAsync_NoTicketId_DoesNotCallTicketFactory_ButStillMarksFailed()
    {
        await _sut.HandleStepFailureAsync(
            Array.Empty<string>(),
            new ResolvedProject(),
            new PipelineContext(),
            _lifecycleMock.Object,
            CommandResult.Fail("boom"),
            CancellationToken.None);

        _ticketFactoryMock.Verify(f => f.Create(It.IsAny<TrackerConnection>()), Times.Never);
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_ReadOnlyPipelineWithRepository_DoesNotPersistWorkBranch()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));

        // Pipeline contains no AgenticExecute / GenerateTests / GenerateDocs.
        var commands = new[] { CommandNames.SpawnNuclei, CommandNames.DeliverFindings };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("scan failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleStepFailureAsync_CommittingPipelineWithRepository_AttemptsPersistWorkBranch()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.SpecSequence, CommandNames.WriteRunResult, CommandNames.CommitAndPR };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("test failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_AgenticMasterPipelineThatCommits_AttemptsPersistWorkBranch()
    {
        // A master run that commits its work (mad-discussion's shape) persists the master's
        // edits; a master alone does not (2026-10-09-af10).
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.AgenticMaster, CommandNames.WriteRunResult, CommandNames.CommitAndPR };

        await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("test failed"),
            CancellationToken.None);

        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStepFailureAsync_PersistThrows_DoesNotMaskOriginalFailure_AndStillMarksFailed()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.Repository,
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        var commands = new[] { CommandNames.AgenticExecute };
        _factoryMock.Setup(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()))
            .Throws(new InvalidOperationException("persist broke"));

        var act = async () => await _sut.HandleStepFailureAsync(
            commands, new ResolvedProject(), context, _lifecycleMock.Object,
            CommandResult.Fail("agentic failed"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        _lifecycleMock.Verify(l => l.MarkFailed(), Times.Once);
    }

    [Fact]
    public async Task PostWorkingStatusAsync_PostsWorkingMessageWhenTicketIdPresent()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("42"));

        await _sut.PostWorkingStatusAsync(new ResolvedProject(), context, CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.Is<TicketId>(id => id.Value == "42"),
            It.Is<string>(s => s.Contains("working on")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PipelineErrorHandler_PostWorkingStatus_PostsWorkingStatusText()
    {
        var ticketProvider = new Mock<ITicketProvider>();
        _ticketFactoryMock.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(ticketProvider.Object);
        var context = new PipelineContext();
        context.Set(ContextKeys.TicketId, new TicketId("42"));

        await _sut.PostWorkingStatusAsync(new ResolvedProject(), context, CancellationToken.None);

        ticketProvider.Verify(t => t.UpdateStatusAsync(
            It.IsAny<TicketId>(),
            AgentSmith.Application.Services.Prompts.OwnTicketComment.WorkingStatus,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
