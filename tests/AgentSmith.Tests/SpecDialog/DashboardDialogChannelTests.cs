using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-9033: the dashboard channel end to end, without a browser — the ingestion
/// endpoint over the REAL spec-dialog router and the REAL durable session store (SQLite
/// in-memory with the shipped migrations), delivering through the real adapter into a
/// recording hub. Only the design turn itself is a stub, so these stay about the channel.
/// </summary>
public sealed class DashboardDialogChannelTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-2f19";
    private const string FreshDialog = "d-fresh";
    private const string Owner = "person-a";
    private const string Intruder = "person-b";
    private const string CannedReply = "canned design answer";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogPendingQuestions _pendingQuestions = new(new SpecDialogTurnGate(TimeProvider.System));
    private readonly SpecDialogTurnGate _turnGate = new(TimeProvider.System);
    private readonly SpecDialogResumer _resumer;
    private readonly Mock<ISpecDialogTurnRunner> _turnRunner = new();
    private readonly Mock<IDialogueTransport> _dialogueTransport = new();
    private readonly RecordingDialogHub _hub = new();
    private readonly RecordingSystemEvents _events = new();
    private readonly SpecDialogOwnership _ownership;
    private readonly ServiceProvider _services;

    public DashboardDialogChannelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();

        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _resumer = new SpecDialogResumer(
            _repository, _turnGate, _pendingQuestions, TimeProvider.System,
            NullLogger<SpecDialogResumer>.Instance);
        _turnRunner
            .Setup(runner => runner.RunTurnAsync(
                It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome()));

        var messenger = new SpecDialogMessenger(
            [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, _hub)],
            NullLogger<SpecDialogMessenger>.Instance);
        _ownership = new SpecDialogOwnership(_repository, new SpecCommandParser());
        _services = Services(new DashboardDialogDispatcher(
            Router(messenger), messenger, NullLogger<DashboardDialogDispatcher>.Instance));
    }

    [Fact]
    public async Task Ingest_SlashSpec_OpensASession()
    {
        var result = await SendAsync("/spec");

        result.Should().BeOfType<Accepted>();
        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        state.Should().NotBeNull();
        state!.UserId.Should().Be(Owner, "the session belongs to the principal that opened it");
        LastText().Should().Contain("opened");
    }

    [Fact]
    public async Task Ingest_AuthenticatedMessage_RoutesThroughTheSpecDialogRouter()
    {
        await SendAsync("/spec");

        await SendAsync("a widget that reads the ledger");

        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Once);
        _hub.Pushes.Last().Group.Should().Be(HubGroups.SpecDialog(Dialog));
        LastText().Should().Be(CannedReply);
        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        state!.Transcript.Select(turn => turn.Text).Should()
            .Equal("a widget that reads the ledger", CannedReply);
    }

    [Fact]
    public async Task Ingest_DoesNotAwaitTheTurnOnTheRequest()
    {
        await SendAsync("/spec");
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _turnRunner
            .Setup(runner => runner.RunTurnAsync(
                It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entered.TrySetResult();
                await release.Task;
                return SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome());
            });

        var opened = _hub.Pushes.Count;
        var result = await Ingest("design something long", Owner);

        result.Should().BeOfType<Accepted>("the turn runs a master and its approval gate "
            + "waits up to fifteen minutes — the request cannot hold that");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _hub.Pushes.Should().HaveCount(opened, "the reply has not been composed yet");
        release.SetResult();
        await Settle(opened + 1);
    }

    [Fact]
    public async Task Ingest_BySomeoneElsesPrincipal_IsRefused()
    {
        await SendAsync("/spec");

        var opened = _hub.Pushes.Count;
        var result = await Ingest("let me approve that for you", Intruder);

        Refusal(result);
        _hub.Pushes.Should().HaveCount(opened,
            "a refused caller is told so by the response, not served the dialog");
        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        state!.Transcript.Should().BeEmpty("nothing of theirs reaches the transcript");
    }

    [Fact]
    public async Task Ingest_ResumingAnotherPrincipalsSession_IsRefused()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var result = await Ingest($"/spec resume {opened!.JobId}", Intruder, "d-elsewhere");

        Refusal(result);
        (await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None))!
            .ThreadId.Should().Be(Dialog, "resume re-binds a session, so it is a takeover too");
    }

    /// <summary>
    /// The door the endpoint's own refusal does not cover. A /spec resume typed in a Slack or
    /// Teams thread reaches this manager directly — no dashboard endpoint, no hub, no
    /// ownership check on that path — and the resume rewrites the session's platform, channel
    /// and thread. Guarding only the dashboard left the session takeable from any chat
    /// workspace by anyone who had seen its id.
    /// </summary>
    [Fact]
    public async Task Resume_ByAChatUserWhoIsNotTheOwner_LeavesTheSessionWhereItIs()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var taken = await _resumer.ResumeAsync(
            opened!.JobId, "U-slack-stranger", "slack", "C-public", "1726500000.0001",
            CancellationToken.None);

        taken.Should().BeOfType<SpecDialogResumeNotFound>(
            "an unowned session answers 'not found', which is also no id oracle");
        var row = await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None);
        row!.Platform.Should().Be(Platform);
        row.ThreadId.Should().Be(Dialog);
        row.UserId.Should().Be(Owner);
    }

    [Fact]
    public async Task Resume_ByItsOwner_MovesTheSessionToTheNewThread()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var resumed = await _resumer.ResumeAsync(
            opened!.JobId, Owner, Platform, "channel", "d-second-tab", CancellationToken.None);

        resumed.Should().BeOfType<SpecDialogResumed>();
        (await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None))!
            .ThreadId.Should().Be("d-second-tab");
    }

    /// <summary>
    /// A running turn holds the old thread. Moved under it, its reply would find no open
    /// session and never be stored, and its approval would be accepted in the new tab and
    /// then fail to file — after the operator approved.
    /// </summary>
    [Fact]
    public async Task Resume_WhileATurnIsRunning_IsRefusedWithAReason()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        var release = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        _turnRunner
            .Setup(runner => runner.RunTurnAsync(
                It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entered.TrySetResult();
                await release.Task;
                return SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome());
            });
        await Ingest("design the widget", Owner);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var before = _hub.Pushes.Count;
        await Ingest($"/spec resume {opened!.JobId}", Owner, FreshDialog);
        await Settle(before + 1);

        LastText().Should().Contain("in the middle of a turn");
        _hub.Pushes.Last().Group.Should().Be(HubGroups.SpecDialog(FreshDialog));
        release.SetResult();
        await Settle(before + 2);
        var row = await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(Dialog, "the conversation stays where its turn is running");
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!
            .Transcript.Select(turn => turn.Text).Should().Contain(CannedReply,
                "the running turn's reply still finds its session");
    }

    [Fact]
    public async Task Resume_WhileAQuestionIsPending_IsRefusedWithAReason()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        _pendingQuestions.Set(opened!.JobId, "q-approval", "file these tickets?");

        await SendAsync($"/spec resume {opened.JobId}", FreshDialog);

        LastText().Should().Contain("waiting for an answer");
        (await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None))!
            .ThreadId.Should().Be(Dialog);
        _pendingQuestions.TryPeek(opened.JobId, out _).Should().BeTrue(
            "the question still waits where it was asked");
    }

    /// <summary>
    /// Opening a past conversation on the dashboard is a resume onto a freshly minted dialog
    /// id. Nothing is open there, so the resume closes nothing — not the conversation that
    /// replaced it on its old tab.
    /// </summary>
    [Fact]
    public async Task Resume_OntoAFreshDialogId_ClosesNothingElse()
    {
        await SendAsync("/spec");
        var past = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        await SendAsync("/spec new");
        ForgetTracked();
        var current = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        await SendAsync($"/spec resume {past!.JobId}", FreshDialog);

        LastText().Should().Contain("resumed");
        (await _sessions.GetOpenByThreadAsync(Platform, FreshDialog, CancellationToken.None))!
            .JobId.Should().Be(past.JobId);
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!
            .JobId.Should().Be(current!.JobId, "the conversation on the other tab stays open");
    }

    /// <summary>
    /// Found by review, and older than this phase: the resume closed the target thread with a bulk
    /// update the tracked session did not see, then set the session open again — which, to the
    /// change tracker, was no change. So a resume into the thread a session already lived in
    /// silently closed it while replying "resumed". The tracker is deliberately NOT cleared here:
    /// clearing it is exactly what hid the bug from the tests beside this one.
    /// </summary>
    [Fact]
    public async Task Resume_IntoTheThreadItAlreadyLivesIn_LeavesItOpen()
    {
        await SendAsync("/spec");
        var here = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        await SendAsync($"/spec resume {here!.JobId}");

        LastText().Should().Contain("resumed");
        _context.ChangeTracker.Clear();
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))
            .Should().NotBeNull("resuming a conversation where it already is must not close it");
    }

    [Fact]
    public async Task Resume_AnotherPrincipalsConversation_IsRefused()
    {
        await SendAsync("/spec");
        var past = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        await SendAsync("/spec new");
        ForgetTracked();

        var taken = await _resumer.ResumeAsync(
            past!.JobId, Intruder, Platform, FreshDialog, FreshDialog, CancellationToken.None);

        taken.Should().BeOfType<SpecDialogResumeNotFound>();
        var row = await _repository.GetBySessionIdAsync(past.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(Dialog);
        row.IsOpen.Should().BeFalse("a closed conversation is reopened by its owner or by nobody");
    }

    /// <summary>
    /// The list is what hands out the ids the takeover above needs, so it stops naming
    /// conversations the caller cannot resume anyway.
    /// </summary>
    [Fact]
    public async Task List_NamesOnlyTheCallersOwnSessions()
    {
        await SendAsync("/spec");

        var mine = await _sessions.ListOpenAsync(Owner, Platform, CancellationToken.None);
        var theirs = await _sessions.ListOpenAsync(Intruder, Platform, CancellationToken.None);

        mine.Should().ContainSingle();
        theirs.Should().BeEmpty("another principal's conversations are not theirs to see");
    }

    [Fact]
    public async Task Subscribe_ToAnotherPrincipalsSession_IsRefused()
    {
        await SendAsync("/spec");

        var act = async () => await Hub(Intruder).SubscribeSpecDialog(Dialog);

        await act.Should().ThrowAsync<HubException>();
        _hub.Joined.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_ByTheOwner_JoinsTheDialogGroup()
    {
        await SendAsync("/spec");

        await Hub(Owner).SubscribeSpecDialog(Dialog);

        _hub.Joined.Should().Equal(HubGroups.SpecDialog(Dialog));
    }

    [Fact]
    public async Task Ingest_WithAPendingTypedQuestion_AnswersItInsteadOfRunningATurn()
    {
        await SendAsync("/spec");
        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        _pendingQuestions.Set(state!.JobId, "q-approval", "approve this?");

        await Ingest("yes", Owner);
        await SettleAnswers();

        _dialogueTransport.Verify(transport => transport.PublishAnswerAsync(
            state.JobId,
            It.Is<DialogAnswer>(answer =>
                answer.QuestionId == "q-approval" && answer.Answer == "yes"),
            It.IsAny<CancellationToken>()), Times.Once);
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ingest_WhileATurnIsRunning_IsToldSoAndDoesNotQueueASecond()
    {
        await SendAsync("/spec");
        var release = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        _turnRunner
            .Setup(runner => runner.RunTurnAsync(
                It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entered.TrySetResult();
                await release.Task;
                return SpecDialogTurnResult.On(Platform, CannedReply, new AnswerOutcome());
            });
        var opened = _hub.Pushes.Count;
        await Ingest("the first thought", Owner);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Ingest("and one more thing", Owner);
        await Settle(opened + 1);

        LastText().Should().Contain("Still working on the previous turn");
        release.SetResult();
        await Settle(opened + 2);
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Once,
            "the second message informs the next turn rather than starting one");
    }

    [Fact]
    public async Task Ingest_WithNoOpenSessionAndNoCommand_IsAnswered()
    {
        await SendAsync("just thinking out loud");

        LastText().Should().Contain("No spec dialog is open here",
            "a dedicated page has no other conversation to fall through to");
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 2026-09-17-042ek: this path exists only on the dashboard, so the one thing it must not
    // do is answer a page with the three slash commands that page has no way to type.
    [Fact]
    public async Task DashboardDialogDispatcher_NoOpenDialog_NamesNoCommand()
    {
        await SendAsync("just thinking out loud");

        var answer = LastText();
        answer.Should().NotContain("/spec");
        answer.Should().Contain("New conversation",
            "the button that starts one is what the page actually offers");
        answer.Should().Contain("project", "the picker decides the scope when there are several");
    }

    [Fact]
    public async Task Ingest_PublishesTheChatIngestionSystemEvent()
    {
        await SendAsync("/spec");

        var published = _events.Published.Should().ContainSingle()
            .Which.Should().BeOfType<ChatMessageReceivedEvent>().Subject;
        published.Source.Should().Be("chat:dashboard");
        published.Channel.Should().Be(Dialog);
        published.Actioned.Should().BeTrue();
        _events.Published.Should().NotContain(
            systemEvent => systemEvent.ToString()!.Contains("/spec", StringComparison.Ordinal),
            "the ingestion event carries metadata only, never the message");
    }

    private SpecDialogRouter Router(SpecDialogMessenger messenger)
    {
        var composer = new SpecDialogReplyComposer();
        var outcomeComposer = new SpecDialogOutcomeComposer();
        var outcomeFlow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                _dialogueTransport.Object, messenger, _pendingQuestions, outcomeComposer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), outcomeComposer, messenger,
new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance, _hub),
            new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<SpecDialogOutcomeFlow>.Instance);
        return new SpecDialogRouter(
            new SpecCommandParser(), _sessions,
            new SpecDialogCommandHandler(
                _sessions, _resumer, new SpecDialogScopeResolver(SingleProjectLoader()),
                composer, messenger),
            _turnRunner.Object, outcomeFlow, _turnGate,
            new SpecDialogAnswerAdmission(_sessions, _pendingQuestions, _dialogueTransport.Object),
            SilentSubjectMinter.Over(_repository),
            new SpecDialogEditReload(_sessions, NullLogger<SpecDialogEditReload>.Instance),
            composer, messenger,
            NullLogger<SpecDialogRouter>.Instance);
    }

    private ServiceProvider Services(DashboardDialogDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(dispatcher);
        services.AddSingleton(_ownership);
        // 2026-09-17-042eg: the endpoint reads whether this caller may start runs before it
        // dispatches. A non-enforcing authority answers without touching the identity resolver,
        // which is the installation these routing tests are about.
        services.AddSingleton(new TokenAuthorityConfig());
        services.AddSingleton<ISystemEventPublisher>(_events);
        return services.BuildServiceProvider();
    }

    // The hub method under test touches none of the readers, so they are absent rather
    // than faked — what it needs is the ownership guard and a caller.
    private JobsHub Hub(string caller) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, _ownership, null!)
        {
            Context = new FakeCaller(Principal(caller)),
            Groups = _hub,
        };

    // Every dispatched message runs in a scope of its own in the server, with a fresh unit of
    // work. Here one context serves them all, and the close is a bulk update its tracker never
    // sees — so a closed row would still read as open from an earlier load.
    private void ForgetTracked() => _context.ChangeTracker.Clear();

    private async Task<IResult> SendAsync(string text, string dialogId = Dialog)
    {
        var before = _hub.Pushes.Count;
        var result = await Ingest(text, Owner, dialogId);
        await Settle(before + 1);
        return result;
    }

    private Task<IResult> Ingest(string text, string caller, string dialogId = Dialog) =>
        SpecDialogEndpoints.IngestAsync(
            new SpecDialogEndpoints.SpecDialogMessageRequest(dialogId, text),
            new DefaultHttpContext { RequestServices = _services, User = Principal(caller) });

    // The endpoint dispatches fire-and-forget, so a test waits for the effect it is about
    // to assert on rather than for a task it was never handed.
    private Task Settle(int pushes) => WaitFor(() => _hub.Pushes.Count >= pushes);

    private Task SettleAnswers() => WaitFor(() => _dialogueTransport.Invocations.Count > 0);

    private static async Task WaitFor(Func<bool> reached)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!reached() && DateTimeOffset.UtcNow < deadline) await Task.Delay(10);
        reached().Should().BeTrue("the dispatched turn never produced what it was waited on for");
    }

    private string LastText() => _hub.Pushes.Last().Args
        .OfType<SpecDialogChannelMessage>().Single().Text;

    private static void Refusal(IResult result) =>
        result.Should().BeOfType<StatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    private static IConfigurationLoader SingleProjectLoader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new() { Name = "sample", Repos = [new RepoConnection { Name = "repo-a" }] },
            },
        });
        return loader.Object;
    }

    public void Dispose()
    {
        _services.Dispose();
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class RecordingSystemEvents : ISystemEventPublisher
    {
        public List<SystemEvent> Published { get; } = [];

        public Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(systemEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCaller(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "c-9033";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features { get; } =
            new Microsoft.AspNetCore.Http.Features.FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
