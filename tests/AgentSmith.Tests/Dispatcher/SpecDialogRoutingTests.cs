using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// p0315a: the SpecDialog routing branch over the REAL durable store (SQLite
/// in-memory with the shipped migrations — a Redis flush cannot touch it).
/// /spec opens a scoped per-thread session, parallel threads stay isolated,
/// resume rebinds a session, and normal messages fall through untouched.
/// </summary>
public sealed class SpecDialogRoutingTests : IDisposable
{
    private const string Platform = "slack";
    private const string Channel = "C1";

    private const string CannedReply = "canned design answer";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogRouter _router;
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly Mock<ISpecDialogTurnRunner> _turnRunner;
    private readonly Mock<AgentSmith.Contracts.Dialogue.IDialogueTransport> _dialogueTransport = new();
    private readonly Mock<IOutcomeSink> _outcomeSink = new();
    private readonly SpecDialogTurnGate _turnGate = new(TimeProvider.System);
    private readonly SpecDialogPendingQuestions _pendingQuestions = new(new SpecDialogTurnGate(TimeProvider.System));

    public SpecDialogRoutingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = NewContext();
        _context.Database.Migrate();

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        var repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            repository, AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        var messenger = new SpecDialogMessenger(
            [_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var turnGate = _turnGate;
        var pendingQuestions = _pendingQuestions;
        var commandHandler = new SpecDialogCommandHandler(
            _sessions,
            new SpecDialogScopeResolver(SingleProjectLoader()),
            new SpecDialogReplyComposer(), messenger);
        // p0315b: follow-up turns now run the design-partner master; the stub
        // returns a canned reply so these ROUTING tests stay about routing.
        // p0315e: the canned turn is an ANSWER outcome, so the outcome flow is
        // a pass-through here — its confirm/sink deps are never touched.
        _turnRunner = new Mock<ISpecDialogTurnRunner>();
        _turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome()));
        var outcomeComposer = new SpecDialogOutcomeComposer();
        var outcomeFlow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                _dialogueTransport.Object,
                messenger, pendingQuestions, outcomeComposer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            _outcomeSink.Object, outcomeComposer, messenger,
new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            new SpecDialogLatestOutcomeStore(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<SpecDialogOutcomeFlow>.Instance);
        _router = new SpecDialogRouter(
            new SpecCommandParser(), _sessions, commandHandler,
            _turnRunner.Object, outcomeFlow, turnGate,
            new SpecDialogAnswerAdmission(_sessions, pendingQuestions, _dialogueTransport.Object),
            SilentSubjectMinter.Over(repository),
            new SpecDialogEditReload(_sessions, NullLogger<SpecDialogEditReload>.Instance),
            new SpecDialogReplyComposer(), messenger, NullLogger<SpecDialogRouter>.Instance);
    }

    [Fact]
    public async Task Router_SpecCommand_OpensScopedThread()
    {
        var handled = await _router.TryRouteAsync(
            "/spec", "U1", Channel, "1111.0001", Platform, false, CancellationToken.None);

        handled.Should().BeTrue();
        var state = await _sessions.GetOpenByThreadAsync(Platform, "1111.0001", CancellationToken.None);
        state.Should().NotBeNull();
        state!.Mode.Should().Be(ConversationMode.SpecDialog);
        state.Scope.Should().NotBeNull();
        state.Scope!.Project.Should().Be("sample");
        state.Scope.Repos.Should().Equal("repo-a");
        _adapter.Verify(a => a.SendInfoAsync(
            Channel, It.IsAny<string>(), It.Is<string>(t => t.Contains("sample")),
            "1111.0001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Router_TwoThreads_KeepIndependentTranscripts()
    {
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-A", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-B", Platform, false, CancellationToken.None);

        await _router.TryRouteAsync("first thought in A", "U1", Channel, "th-A", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("only thought in B", "U1", Channel, "th-B", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("second thought in A", "U1", Channel, "th-A", Platform, false, CancellationToken.None);

        var stateA = await _sessions.GetOpenByThreadAsync(Platform, "th-A", CancellationToken.None);
        var stateB = await _sessions.GetOpenByThreadAsync(Platform, "th-B", CancellationToken.None);
        stateA!.Transcript.Where(t => t.Role == TranscriptRole.User).Select(t => t.Text)
            .Should().Equal("first thought in A", "second thought in A");
        stateB!.Transcript.Where(t => t.Role == TranscriptRole.User).Select(t => t.Text)
            .Should().Equal("only thought in B");
        // p0315b: each user turn now gets a persisted assistant reply.
        stateA.Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text)
            .Should().Equal(CannedReply, CannedReply);
        stateA.JobId.Should().NotBe(stateB.JobId, "each thread has its own session");
    }

    // p0315c "edit iterates": a non-approval confirmation reply is an edit
    // note — the router re-runs the design turn over the refreshed transcript
    // and NOTHING reaches the outcome sink.
    [Fact]
    public async Task Router_EditNote_RerunsTurnAndFilesNothing()
    {
        var draft = new PhaseDraft("p9999", "widget goal", "phase: p9999\ngoal: \"widget goal\"", []);
        _turnRunner.SetupSequence(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "draft reply", new PhaseOutcome(draft)))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "revised reply", new AnswerOutcome()));
        _dialogueTransport.Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Dialogue.DialogAnswer(
                "q1", "split it into two slices", null, DateTimeOffset.UtcNow, "U1"));

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-edit", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("draft the phase", "U1", Channel, "th-edit", Platform, false, CancellationToken.None);

        _turnRunner.Verify(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2), "the edit note re-prompts the master with a fresh turn");
        _outcomeSink.Verify(s2 => s2.AcceptAsync(
                It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), false, It.IsAny<CancellationToken>()),
            Times.Never, "an edited proposal is never filed");
        var state = await _sessions.GetOpenByThreadAsync(Platform, "th-edit", CancellationToken.None);
        state!.Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text)
            .Should().Contain(["draft reply", "revised reply"]);
    }

    // 2026-09-17-042ec: the router records what the turn produced on the kept turn.
    [Fact]
    public async Task Router_KeptTurn_RecordsTheKindTheTurnProduced()
    {
        _turnRunner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "turn failed", new AnswerOutcome(), SpecDialogTurnKind.Failure));

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-kind", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("update it all", "U1", Channel, "th-kind", Platform, false, CancellationToken.None);

        var state = await _sessions.GetOpenByThreadAsync(Platform, "th-kind", CancellationToken.None);
        state!.Transcript.Select(t => (t.Role, t.Kind)).Should().Equal(
            (TranscriptRole.User, (SpecDialogTurnKind?)null), (TranscriptRole.Assistant, SpecDialogTurnKind.Failure));
    }

    // 2026-09-17-042el: an edit note typed at the approval gate is routed as the answer, and the turn
    // it re-runs reads a transcript that already ends with the note — the append precedes the publish.
    [Fact]
    public async Task Router_EditNoteAnswer_ReRunsOverATranscriptEndingInTheNote()
    {
        const string note = "cut it into two slices";
        var draft = new PhaseDraft("p9999", "widget goal", "phase: p9999\ngoal: \"widget goal\"", []);
        var reRun = new TaskCompletionSource<ConversationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var noteRouted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<AgentSmith.Contracts.Dialogue.DialogAnswer?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var turns = 0;
        _turnRunner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConversationState state, CancellationToken _) =>
            {
                if (Interlocked.Increment(ref turns) == 1)
                    return SpecDialogTurnResult.On(Platform, "draft reply", new PhaseOutcome(draft));
                reRun.TrySetResult(state);
                await noteRouted.Task;
                return SpecDialogTurnResult.On(Platform, "revised reply", new AnswerOutcome());
            });
        _dialogueTransport.Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(answer.Task);
        // The publish holds its caller until the re-run has read the transcript — awaited or not, a
        // publish ahead of the append is then seen — and the store is never used by both routes at once.
        _dialogueTransport.Setup(t => t.PublishAnswerAsync(
                It.IsAny<string>(), It.IsAny<AgentSmith.Contracts.Dialogue.DialogAnswer>(), It.IsAny<CancellationToken>()))
            .Returns((string _, AgentSmith.Contracts.Dialogue.DialogAnswer published, CancellationToken _) =>
            {
                answer.TrySetResult(published);
                reRun.Task.Wait(TestWaits.Hang);
                return Task.CompletedTask;
            });
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-note", Platform, false, CancellationToken.None);
        var sessionId = (await _sessions.GetOpenByThreadAsync(Platform, "th-note", CancellationToken.None))!.JobId;
        var proposing = Task.Run(() => _router.TryRouteAsync(
            "draft the phase", "U1", Channel, "th-note", Platform, false, CancellationToken.None));
        await TestWaits.UntilAsync(
            () => _pendingQuestions.TryPeek(sessionId, out _), "the proposal reaches the approval gate");

        (await _router.TryRouteAsync(note, "U1", Channel, "th-note", Platform, false, CancellationToken.None))
            .Should().BeTrue();

        var reRunState = await reRun.Task.OrHang("the note re-runs the turn");
        noteRouted.SetResult();
        reRunState.Transcript.Last(turn => turn.Role == TranscriptRole.User).Text.Should().Be(note);
        (await proposing.OrHang("the proposal route returns")).Should().BeTrue();
    }

    // 2026-09-22-355b: a shape PICKED on a chat surface completes the pending question inside
    // that surface's own adapter and is published straight onto the transport, so it never
    // passes the answer admission that appends. Without carrying the note as a value the
    // re-run reads a byte-identical transcript, spends a whole master loop plus a proposal
    // review on it, and returns the same cut under "revising the proposal".
    [Fact]
    public async Task Router_AShapePickedByButton_ReRunsOverATranscriptEndingInThatShape()
    {
        const string shape = "Cut into several phases";
        var draft = new PhaseDraft("p9999", "widget goal", "phase: p9999\ngoal: \"widget goal\"", []);
        ConversationState? reRun = null;
        var turns = 0;
        _turnRunner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns((ConversationState state, CancellationToken _) =>
            {
                if (Interlocked.Increment(ref turns) == 1)
                    return Task.FromResult(
                        SpecDialogTurnResult.On(Platform, "draft reply", new PhaseOutcome(draft)));
                reRun = state;
                return Task.FromResult(
                    SpecDialogTurnResult.On(Platform, "revised reply", new AnswerOutcome()));
            });
        // The button's answer arrives on the transport ALONE — the adapter completed the
        // question itself, so nothing was ever appended on its way in.
        _dialogueTransport.Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Dialogue.DialogAnswer(
                "q1", shape, null, DateTimeOffset.UtcNow, "U1"));

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-shape", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("draft the phase", "U1", Channel, "th-shape", Platform, false, CancellationToken.None);

        reRun.Should().NotBeNull("the picked shape is an edit note, so the turn runs again");
        reRun!.Transcript[^1].Should().Match<TranscriptTurn>(
            turn => turn.Role == TranscriptRole.User && turn.Text == shape,
            "the master must read the chosen shape as the latest user turn");
        reRun.Revising.Should().BeOfType<PhaseOutcome>("it knows what it is re-cutting");
        var stored = await _sessions.GetOpenByThreadAsync(Platform, "th-shape", CancellationToken.None);
        stored!.Transcript.Count(t => t.Text == shape).Should().Be(1);
    }

    // The other half of the same wire: a shape TYPED as a message was already appended by the
    // answer admission, so carrying it must not put it in the transcript a second time.
    [Fact]
    public async Task Router_AShapeTypedAsAMessage_DoesNotAppendItTwice()
    {
        const string shape = "Make it a bug ticket";
        var draft = new PhaseDraft("p9999", "widget goal", "phase: p9999\ngoal: \"widget goal\"", []);
        var answer = new TaskCompletionSource<AgentSmith.Contracts.Dialogue.DialogAnswer?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var turns = 0;
        _turnRunner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns((ConversationState _, CancellationToken __) => Task.FromResult(
                Interlocked.Increment(ref turns) == 1
                    ? SpecDialogTurnResult.On(Platform, "draft reply", new PhaseOutcome(draft))
                    : SpecDialogTurnResult.On(Platform, "revised reply", new AnswerOutcome())));
        _dialogueTransport.Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(answer.Task);
        _dialogueTransport.Setup(t => t.PublishAnswerAsync(
                It.IsAny<string>(), It.IsAny<AgentSmith.Contracts.Dialogue.DialogAnswer>(), It.IsAny<CancellationToken>()))
            .Returns((string _, AgentSmith.Contracts.Dialogue.DialogAnswer published, CancellationToken _) =>
            {
                answer.TrySetResult(published);
                return Task.CompletedTask;
            });

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-typed", Platform, false, CancellationToken.None);
        var sessionId = (await _sessions.GetOpenByThreadAsync(Platform, "th-typed", CancellationToken.None))!.JobId;
        var proposing = Task.Run(() => _router.TryRouteAsync(
            "draft the phase", "U1", Channel, "th-typed", Platform, false, CancellationToken.None));
        await TestWaits.UntilAsync(
            () => _pendingQuestions.TryPeek(sessionId, out _),
            "the proposal reaches the approval gate");

        (await _router.TryRouteAsync(shape, "U1", Channel, "th-typed", Platform, false, CancellationToken.None))
            .Should().BeTrue();
        (await proposing.OrHang("the held proposal turn returns once the shape is answered"))
            .Should().BeTrue();

        var stored = await _sessions.GetOpenByThreadAsync(Platform, "th-typed", CancellationToken.None);
        stored!.Transcript.Count(t => t.Text == shape).Should().Be(1,
            "the answer admission already appended it on its way in");
    }

    // 2026-09-17-042el: a question is pending only while a turn holds the gate, so the answer
    // must not wait for the gate.
    [Fact]
    public async Task Router_AnswerWhileATurnHoldsTheGate_IsStillAnAnswer()
    {
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-gate", Platform, false, CancellationToken.None);
        var opened = await _sessions.GetOpenByThreadAsync(Platform, "th-gate", CancellationToken.None);
        _turnGate.TryEnter(opened!.JobId).Should().BeTrue();
        _pendingQuestions.Set(opened.JobId, new AgentSmith.Contracts.Dialogue.DialogQuestion(
            "q-gate", AgentSmith.Contracts.Dialogue.QuestionType.Approval, "file it?",
            null, null, "", TimeSpan.FromMinutes(15)), expiresAt: null);
        var sentBefore = _adapter.Invocations.Count;

        var handled = await _router.TryRouteAsync("approve", "U1", Channel, "th-gate", Platform, false, CancellationToken.None);

        handled.Should().BeTrue();
        _adapter.Invocations.Count.Should().Be(sentBefore, "an answer is not told a turn is in progress");
        _dialogueTransport.Verify(t => t.PublishAnswerAsync(opened.JobId,
            It.Is<AgentSmith.Contracts.Dialogue.DialogAnswer>(a => a.QuestionId == "q-gate"),
            It.IsAny<CancellationToken>()), Times.Once);
        _turnRunner.Verify(r => r.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
        var state = await _sessions.GetOpenByThreadAsync(Platform, "th-gate", CancellationToken.None);
        state!.Transcript.Should().ContainSingle().Which.Decision.Should().Be(SpecDialogDecision.Approved);
    }

    [Fact]
    public async Task Router_NormalMessage_StillRoutesToRunTrigger()
    {
        var handled = await _router.TryRouteAsync(
            "fix #42 in sample", "U1", Channel, "3333.0003", Platform, false, CancellationToken.None);

        handled.Should().BeFalse("without an open spec thread the message goes to the intent path");
        var intent = new ChatIntentParser(NullLogger<ChatIntentParser>.Instance)
            .Parse("fix #42 in sample", "U1", Channel, Platform);
        intent.Should().BeOfType<FixTicketIntent>("the run-trigger parse is untouched");
        _adapter.Verify(a => a.SendInfoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private AgentSmithDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static IConfigurationLoader SingleProjectLoader()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new()
                {
                    Name = "sample",
                    Repos = [new RepoConnection { Name = "repo-a" }],
                },
            },
        };
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return loader.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
