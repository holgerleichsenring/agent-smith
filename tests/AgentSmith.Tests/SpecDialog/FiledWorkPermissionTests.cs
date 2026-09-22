using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: moving a filed ticket into a trigger status STARTS A RUN, so it is held to
/// runs.control — the permission the retry endpoint that makes the same move already needs, while
/// posting into a dialog needs dialog.write alone. The bool travels in process through the turn
/// that files, sampled on the principal that started it, and both chat channels pass false.
/// </summary>
public sealed class FiledWorkPermissionTests : IDisposable
{
    private const string Dashboard = DispatcherDefaults.PlatformDashboard;
    private const string Dialog = "d-1";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogRouter _router;
    private readonly Mock<IOutcomeSink> _sink = new();
    private readonly Mock<ISpecDialogTurnRunner> _turnRunner = new();
    private readonly SpecDialogTurnGate _turnGate = new(TimeProvider.System);
    private readonly SpecDialogPendingQuestions _pending = new(new SpecDialogTurnGate(TimeProvider.System));

    public FiledWorkPermissionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _router = Router(_repository);
    }

    [Fact]
    public void Ingest_AuthorityNotEnforcing_CarriesMayStartRuns()
    {
        var ctx = Context(new TokenAuthorityConfig { Enforce = false }, permissions: []);

        SpecDialogEndpoints.MayStartRuns(ctx).Should().BeTrue(
            "an installation that enforces nothing holds every caller to nothing, as the hub filter reads it");
    }

    [Fact]
    public void Ingest_EnforcingWithoutRunsControl_CarriesFalse()
    {
        var auth = new TokenAuthorityConfig { Enforce = true };

        SpecDialogEndpoints.MayStartRuns(Context(auth, [Permissions.DialogWrite])).Should().BeFalse(
            "dialog.write posts into the conversation; it does not start runs");
    }

    [Fact]
    public void Ingest_EnforcingWithRunsControl_CarriesTrue()
    {
        var auth = new TokenAuthorityConfig { Enforce = true };

        SpecDialogEndpoints.MayStartRuns(Context(auth, [Permissions.RunsControl])).Should().BeTrue();
    }

    [Fact]
    public async Task DashboardDispatch_WithThePermission_ReachesTheFilerAsTrue()
    {
        await DispatchAsync(mayStartRuns: true);

        _sink.Verify(s => s.AcceptAsync(
            It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DashboardDispatch_WithoutThePermission_ReachesTheFilerAsFalse()
    {
        await DispatchAsync(mayStartRuns: false);

        _sink.Verify(s => s.AcceptAsync(
            It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Slack and Teams reach the router through one call, and it passes false: a chat message
    /// carries no permission this process can check, so approving there files the work and leaves
    /// the ticket exactly where the tracker created it.
    /// </summary>
    [Fact]
    public async Task ChatDispatch_NeverMovesATicket()
    {
        var dispatcher = new SlackMessageDispatcher(
            null!, null!, null!, null!, null!, null!, null!, _router, null!,
            NullLogger<SlackMessageDispatcher>.Instance);

        await dispatcher.DispatchAsync("/spec", "U1", "C1", CancellationToken.None, "th-1", "slack");
        await dispatcher.DispatchAsync("draft it", "U1", "C1", CancellationToken.None, "th-1", "slack");

        _sink.Verify(s => s.AcceptAsync(
            It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The permission the APPROVING request carries reaches nothing. An approval is admitted as
    /// the answer to the pending question and returns before RunTurnAsync, so the bool that
    /// governs the move is the one sampled when the turn that PROPOSED started, up to the
    /// confirmer's fifteen minutes earlier. Both directions follow: a role removed in between
    /// still moves the ticket, and a role GRANTED in between changes nothing — which is why the
    /// not-started reason names a move in the tracker rather than a colleague to wait for.
    /// </summary>
    [Fact]
    public async Task Approval_TheAnswerPost_CarriesNoPermissionOfItsOwn()
    {
        await _router.TryRouteAsync("/spec", "U1", "C1", "th-2", "slack", false, CancellationToken.None);
        var state = await _sessions.GetOpenByThreadAsync("slack", "th-2", CancellationToken.None);
        // The gate is deliberately FREE: holding it would stop the turn for a second reason and
        // hide whether the answer branch is what returned.
        _pending.Set(state!.JobId, new DialogQuestion(
            "q-1", QuestionType.Approval, "file it?", null, null, "", TimeSpan.FromMinutes(15)),
            expiresAt: null);

        var handled = await _router.TryRouteAsync(
            "approve", "U1", "C1", "th-2", "slack", true, CancellationToken.None);

        handled.Should().BeTrue();
        _turnRunner.Verify(r => r.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
        _sink.Verify(s => s.AcceptAsync(
            It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "the approving message only answers the pending question; the filing runs inside the "
            + "turn that proposed it, with the permission that turn was opened with");
    }

    private async Task DispatchAsync(bool mayStartRuns)
    {
        var dispatcher = new DashboardDialogDispatcher(
            _router, Conversations(), Messenger(), NullLogger<DashboardDialogDispatcher>.Instance);
        await dispatcher.DispatchAsync(
            Dialog, "/spec", "U1", mayStartRuns, null, CancellationToken.None);
        await dispatcher.DispatchAsync(
            Dialog, "draft it", "U1", mayStartRuns, null, CancellationToken.None);
        (await _sessions.GetOpenByThreadAsync(Dashboard, Dialog, CancellationToken.None))
            .Should().NotBeNull("the turn that files runs inside an open session");
    }

    private static HttpContext Context(TokenAuthorityConfig auth, string[] permissions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(auth);
        services.AddSingleton(ResolverUnderTest.With(auth));
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = ResolverUnderTest.Caller(
                auth, [.. permissions.Select(p => (PermissionClaims.Type, p))]),
        };
    }

    /// <summary>The dispatcher resolves-or-opens before it routes. These dispatches name no
    /// project, so nothing is opened here — but the collaborator is real rather than null, so a
    /// path that started consulting it would be exercised instead of throwing.</summary>
    private SpecDialogConversationResolver Conversations() =>
        new(_sessions,
            new SpecDialogOwnership(_repository),
            new SpecDialogCommandHandler(
                _sessions, new SpecDialogScopeResolver(Loader()),
                new SpecDialogReplyComposer(), Messenger()));

    private static SpecDialogMessenger Messenger() =>
        new([], NullLogger<SpecDialogMessenger>.Instance);

    /// <summary>The real router over a turn that proposes a phase and a gate that approves it —
    /// the shortest path from "a message arrived" to "the filer was handed a permission".</summary>
    private SpecDialogRouter Router(SpecDialogSessionRepository repository)
    {
        var messenger = Messenger();
        var composer = new SpecDialogOutcomeComposer();
        var turnGate = _turnGate;
        var pending = _pending;
        var transport = new Mock<IDialogueTransport>();
        transport
            .Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string questionId, TimeSpan _, CancellationToken _) =>
                new DialogAnswer(questionId, "approve", null, DateTimeOffset.UtcNow, "U1"));
        var turnRunner = _turnRunner;
        turnRunner
            .Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState state, CancellationToken _) =>
                SpecDialogTurnResult.On(state.Platform, "drafted", new PhaseOutcome(
                    new PhaseDraft("p9000a", "a slice",
                        "phase: p9000a\ngoal: \"a slice\"\ndone:\n  - \"done\"", [])
                    { Done = ["done"] })));
        var flow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                transport.Object, messenger, pending, composer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            _sink.Object, composer, messenger,
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            new SpecDialogLatestOutcomeStore(repository, NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<SpecDialogOutcomeFlow>.Instance);
        return new SpecDialogRouter(
            new SpecCommandParser(), _sessions,
            new SpecDialogCommandHandler(
                _sessions, new SpecDialogScopeResolver(Loader()),
                new SpecDialogReplyComposer(), messenger),
            turnRunner.Object, flow, turnGate,
            new SpecDialogAnswerAdmission(_sessions, pending, transport.Object),
            SilentSubjectMinter.Over(repository, "proj"),
            new SpecDialogEditReload(_sessions, NullLogger<SpecDialogEditReload>.Instance),
            new SpecDialogReplyComposer(), messenger, NullLogger<SpecDialogRouter>.Instance);
    }

    private static IConfigurationLoader Loader()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["proj"] = new() { Name = "proj", Repos = [new RepoConnection { Name = "sample-api" }] },
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
