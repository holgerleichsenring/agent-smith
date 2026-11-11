using AgentSmith.Tests.TestSupport;
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
    private readonly AgentSmith.Tests.TestSupport.SpecDialogDecisionHarness _harness;
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
        MigratedStoreTemplate.CopyInto(_context);

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        var repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(repository), AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
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
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome()));
        var outcomeComposer = new SpecDialogOutcomeComposer();
        _harness = new AgentSmith.Tests.TestSupport.SpecDialogDecisionHarness(_context, null, _outcomeSink.Object, messenger);

        _router = _harness.Router(commandHandler, _turnRunner.Object, turnGate, new SpecDialogAnswerAdmission(_sessions, pendingQuestions, _dialogueTransport.Object), SilentTurnKeeper.Over(repository, _sessions, _context));
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

    // 2026-11-04-96a0b: a set stored while the turn runs is one the turn never listed. The notice is kept and sent
    // BEFORE the reply, so the page shows it live and the reply stays the last entry its proposal card attaches to.
    [Fact]
    public async Task SpecDialogRouter_UploadStoredDuringTheTurn_KeepsAndSendsANoticeBeforeTheReply()
    {
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-up", Platform, false, CancellationToken.None);
        var sent = new List<string>();
        _adapter.Setup(a => a.SendInfoAsync(
                Channel, It.IsAny<string>(), It.IsAny<string>(), "th-up", It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string text, string? _, CancellationToken _) => sent.Add(text))
            .Returns(Task.CompletedTask);
        _turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .Returns(async (ConversationState state, TurnPoster _, CancellationToken ct) =>
            {
                await new ReferenceSetRepository(_context).AddAsync(state.JobId,
                    [new() { RelativePath = "app/a.cs", MediaType = "text/plain", Content = [0x61] }], ct);
                return SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome());
            });

        await _router.TryRouteAsync("what does the app do?", "U1", Channel, "th-up", Platform, false, CancellationToken.None);

        const string Notice = "reference:app (1 file) was uploaded while this answer was being written, so the answer "
            + "did not read it. The next answer can read it.";
        sent.Should().Equal(Notice, CannedReply);
        var kept = (await _sessions.GetOpenByThreadAsync(Platform, "th-up", CancellationToken.None))!.Transcript
            .Where(t => t.Role == TranscriptRole.Assistant).ToList();
        kept.Select(t => (t.Text, t.Kind)).Should().Equal(
            (Notice, SpecDialogTurnKind.Notice), (CannedReply, SpecDialogTurnKind.Answer));
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

    // 2026-11-10-ccd9: no edit loop — a message after a proposal is an ordinary message that runs an ordinary
    // turn; the proposal stays awaiting and nothing reaches the outcome sink.
    [Fact]
    public async Task Router_ANoteAfterAProposal_RunsAnOrdinaryTurnAndFilesNothing()
    {
        ProposeThenAnswer();

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-edit", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("draft the spec", "U1", Channel, "th-edit", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("split it into two slices", "U1", Channel, "th-edit", Platform, false, CancellationToken.None);

        _turnRunner.Verify(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2), "the note is a message, and a message runs a turn");
        _outcomeSink.Verify(s2 => s2.AcceptAsync(
                It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never, "nothing is filed without an approve");
        (await _harness.Outcomes.ReadAsync(Platform, "th-edit", CancellationToken.None)).ProposalId
            .Should().NotBeNull("an answer turn leaves the proposal awaiting");
    }

    // 2026-11-10-ccd9: an approve carrying the card's id decides it inside the gate and runs no turn.
    [Fact]
    public async Task Router_ApproveWithTheCardsId_FilesAndRunsNoTurn()
    {
        ProposeThenAnswer();
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-ok", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("draft the spec", "U1", Channel, "th-ok", Platform, false, CancellationToken.None);
        var id = (await _harness.Outcomes.ReadAsync(Platform, "th-ok", CancellationToken.None)).ProposalId;

        await _router.TryRouteAsync("approve", "U1", Channel, "th-ok", Platform, true, CancellationToken.None, proposalId: id);

        _turnRunner.Verify(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()),
            Times.Once, "a decision runs no turn");
        _outcomeSink.Verify(s2 => s2.AcceptAsync(
                It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), true, It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Once, "the decider's own right to start runs goes with the filing");
    }

    // 2026-11-10-ccd9: a decision on a card that is no longer the awaiting one decides nothing and is told so.
    [Fact]
    public async Task Router_ApproveWithAnOlderId_IsToldAndRunsNoTurn()
    {
        ProposeThenAnswer();
        await _router.TryRouteAsync("/spec", "U1", Channel, "th-old", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("draft the spec", "U1", Channel, "th-old", Platform, false, CancellationToken.None);

        await _router.TryRouteAsync("approve", "U1", Channel, "th-old", Platform, true, CancellationToken.None, proposalId: "gone");

        _outcomeSink.Verify(s2 => s2.AcceptAsync(
                It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
        var state = await _sessions.GetOpenByThreadAsync(Platform, "th-old", CancellationToken.None);
        state!.Transcript.Should().Contain(t => t.Kind == SpecDialogTurnKind.Notice && t.Text.Contains("replaced"));
    }

    private void ProposeThenAnswer()
    {
        var draft = new SpecDraft("p9999", "widget goal", "spec: p9999\ngoal: \"widget goal\"", []);
        _turnRunner.SetupSequence(r =>
                r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "draft reply", new SpecOutcome(draft)))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "revised reply", new AnswerOutcome()));
    }

    // 2026-09-17-042ec: the router records what the turn produced on the kept turn.
    [Fact]
    public async Task Router_KeptTurn_RecordsTheKindTheTurnProduced()
    {
        _turnRunner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, "turn failed", new AnswerOutcome(), SpecDialogTurnKind.Failure));

        await _router.TryRouteAsync("/spec", "U1", Channel, "th-kind", Platform, false, CancellationToken.None);
        await _router.TryRouteAsync("update it all", "U1", Channel, "th-kind", Platform, false, CancellationToken.None);

        var state = await _sessions.GetOpenByThreadAsync(Platform, "th-kind", CancellationToken.None);
        state!.Transcript.Select(t => (t.Role, t.Kind)).Should().Equal(
            (TranscriptRole.User, (SpecDialogTurnKind?)null), (TranscriptRole.Assistant, SpecDialogTurnKind.Failure));
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
        _turnRunner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()), Times.Never);
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
