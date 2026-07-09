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
        var commandHandler = new SpecDialogCommandHandler(
            _sessions, new SpecDialogScopeResolver(SingleProjectLoader()),
            new SpecDialogReplyComposer(), messenger);
        // p0315b: follow-up turns now run the design-partner master; the stub
        // returns a canned reply so these ROUTING tests stay about routing.
        // p0315e: the canned turn is an ANSWER outcome, so the outcome flow is
        // a pass-through here — its confirm/sink deps are never touched.
        _turnRunner = new Mock<ISpecDialogTurnRunner>();
        _turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecDialogTurnResult(CannedReply, new AnswerOutcome()));
        var outcomeComposer = new SpecDialogOutcomeComposer();
        var outcomeFlow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>(),
                new SpecDialogQuestionPump(
                    Mock.Of<IMessageBus>(), messenger, new SpecDialogPendingQuestions(),
                    new SpecDialogReplyComposer(), NullLogger<SpecDialogQuestionPump>.Instance),
                new SpecDialogPendingQuestions(), outcomeComposer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), outcomeComposer, messenger,
            NullLogger<SpecDialogOutcomeFlow>.Instance);
        _router = new SpecDialogRouter(
            new SpecCommandParser(), _sessions, commandHandler,
            _turnRunner.Object, outcomeFlow, new SpecDialogTurnGate(), new SpecDialogPendingQuestions(),
            Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>(),
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
