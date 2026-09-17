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
    private readonly SpecDialogTurnGate _turnGate = new();
    private readonly SpecDialogPendingQuestions _pendingQuestions = new();

    public SpecDialogRoutingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = NewContext();
        _context.Database.Migrate();

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        var repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        var messenger = new SpecDialogMessenger(
            [_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var turnGate = _turnGate;
        var pendingQuestions = _pendingQuestions;
        var commandHandler = new SpecDialogCommandHandler(
            _sessions,
            new SpecDialogResumer(repository, turnGate, pendingQuestions, TimeProvider.System,
                NullLogger<SpecDialogResumer>.Instance),
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
            new SpecDialogReplyComposer(), messenger, NullLogger<SpecDialogRouter>.Instance);
    }

    [Fact]
    public async Task Router_SpecCommand_OpensScopedThread()
    {
        var handled = await _router.TryRouteAsync(
            "/spec", "U1", Channel, "1111.0001", Platform, CancellationToken.None);

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
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-A", Platform, CancellationToken.None);
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-B", Platform, CancellationToken.None);

        await _router.TryRouteAsync("first thought in A", "U1", Channel, "th-A", Platform, CancellationToken.None);
        await _router.TryRouteAsync("only thought in B", "U1", Channel, "th-B", Platform, CancellationToken.None);
        await _router.TryRouteAsync("second thought in A", "U1", Channel, "th-A", Platform, CancellationToken.None);

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

    [Fact]
    public async Task Router_ResumeThread_ContinuesWhereLeftOff()
    {
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-old", Platform, CancellationToken.None);
        await _router.TryRouteAsync("first thought", "U1", Channel, "th-old", Platform, CancellationToken.None);
        var opened = await _sessions.GetOpenByThreadAsync(Platform, "th-old", CancellationToken.None);

        var handled = await _router.TryRouteAsync(
            $"/spec resume {opened!.JobId}", "U1", Channel, "th-new", Platform, CancellationToken.None);
        await _router.TryRouteAsync("second thought", "U1", Channel, "th-new", Platform, CancellationToken.None);

        handled.Should().BeTrue();
        var resumed = await _sessions.GetOpenByThreadAsync(Platform, "th-new", CancellationToken.None);
        resumed!.JobId.Should().Be(opened.JobId, "resume continues the same session");
        resumed.Transcript.Where(t => t.Role == TranscriptRole.User).Select(t => t.Text)
            .Should().Equal("first thought", "second thought");
        (await _sessions.GetOpenByThreadAsync(Platform, "th-old", CancellationToken.None))
            .Should().BeNull("the session moved to the new thread");
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

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-edit", Platform, CancellationToken.None);
        await _router.TryRouteAsync("draft the phase", "U1", Channel, "th-edit", Platform, CancellationToken.None);

        _turnRunner.Verify(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2), "the edit note re-prompts the master with a fresh turn");
        _outcomeSink.Verify(s2 => s2.AcceptAsync(
                It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<CancellationToken>()),
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

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-kind", Platform, CancellationToken.None);
        await _router.TryRouteAsync("update it all", "U1", Channel, "th-kind", Platform, CancellationToken.None);

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
                reRun.Task.Wait(TimeSpan.FromSeconds(10));
                return Task.CompletedTask;
            });
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-note", Platform, CancellationToken.None);
        var sessionId = (await _sessions.GetOpenByThreadAsync(Platform, "th-note", CancellationToken.None))!.JobId;
        var proposing = Task.Run(() => _router.TryRouteAsync(
            "draft the phase", "U1", Channel, "th-note", Platform, CancellationToken.None));
        for (var waited = 0; !_pendingQuestions.TryPeek(sessionId, out _); waited++)
        {
            waited.Should().BeLessThan(1000, "the proposal reaches the approval gate");
            await Task.Delay(10);
        }

        (await _router.TryRouteAsync(note, "U1", Channel, "th-note", Platform, CancellationToken.None))
            .Should().BeTrue();

        var reRunState = await reRun.Task.WaitAsync(TimeSpan.FromSeconds(10));
        noteRouted.SetResult();
        reRunState.Transcript.Last(turn => turn.Role == TranscriptRole.User).Text.Should().Be(note);
        (await proposing.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
    }

    // 2026-09-17-042el: a question is pending only while a turn holds the gate, so the answer
    // must not wait for the gate.
    [Fact]
    public async Task Router_AnswerWhileATurnHoldsTheGate_IsStillAnAnswer()
    {
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-gate", Platform, CancellationToken.None);
        var opened = await _sessions.GetOpenByThreadAsync(Platform, "th-gate", CancellationToken.None);
        _turnGate.TryEnter(opened!.JobId).Should().BeTrue();
        _pendingQuestions.Set(opened.JobId, new AgentSmith.Contracts.Dialogue.DialogQuestion(
            "q-gate", AgentSmith.Contracts.Dialogue.QuestionType.Approval, "file it?",
            null, null, "", TimeSpan.FromMinutes(15)), expiresAt: null);
        var sentBefore = _adapter.Invocations.Count;

        var handled = await _router.TryRouteAsync("approve", "U1", Channel, "th-gate", Platform, CancellationToken.None);

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
            "fix #42 in sample", "U1", Channel, "3333.0003", Platform, CancellationToken.None);

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
