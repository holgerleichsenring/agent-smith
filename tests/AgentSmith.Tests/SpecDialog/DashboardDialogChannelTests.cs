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
    private const string FirstSentence = "a widget that reads the ledger";
    private const string NoOpenDialog = "No spec dialog is open here";

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
            _repository, AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
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
        _ownership = new SpecDialogOwnership(_repository);
        _services = Services(new DashboardDialogDispatcher(
            Router(messenger),
            new SpecDialogConversationResolver(_sessions, _ownership, Commands(messenger)),
            messenger, NullLogger<DashboardDialogDispatcher>.Instance));
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
        await entered.Task.OrHang("the dispatched turn enters the runner");
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

    /// <summary>
    /// 2026-09-22-2a86: the page no longer posts "/spec resume &lt;id&gt;" at its own server, so
    /// the resume is a ROUTE — and the guard that used to be reached by parsing that text is
    /// carried by the route explicitly. These pin both sides of it over the real store.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_AConversationTheCallerOwns_IsResumedOntoTheTargetDialog()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var result = await Resume(opened!.JobId, FreshDialog);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        var row = await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(FreshDialog, "the conversation moved onto the tab that asked for it");
        row.ChannelId.Should().Be(FreshDialog, "a page has no channel above the dialog it holds");
        row.IsOpen.Should().BeTrue();
    }

    /// <summary>
    /// The SOURCE side. The lookup is by session id alone and the resume rewrites where the
    /// session lives, so without this an id anybody had seen was a conversation anybody could
    /// take. "Not found" is also no oracle for which ids exist.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_AConversationTheCallerDoesNotOwn_IsNotFound()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var result = await Resume(opened!.JobId, "d-elsewhere", Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        var row = await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(Dialog, "a resume re-binds a session, so it is a takeover too");
        row.UserId.Should().Be(Owner);
    }

    /// <summary>
    /// The TARGET side, and the reason this phase is not a deletion. The resumer's own guards
    /// are all about the source session; the move CLOSES whatever is open on the target thread
    /// before rebinding. A route addressed by ids that did not check the target would let a
    /// caller close another principal's live dialog with a conversation of their own.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_ATargetDialogTheCallerMayNotWatch_IsRefusedAndClosesNothing()
    {
        await SendAsync("/spec");
        var mine = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        var before = _hub.Pushes.Count;
        await Ingest("/spec", Intruder, FreshDialog);
        await Settle(before + 1);
        var theirs = await _sessions.GetOpenByThreadAsync(Platform, FreshDialog, CancellationToken.None);

        var result = await Resume(mine!.JobId, FreshDialog);

        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
        ForgetTracked();
        (await _sessions.GetOpenByThreadAsync(Platform, FreshDialog, CancellationToken.None))!
            .JobId.Should().Be(theirs!.JobId, "the dialog they are talking in is still open");
        (await _repository.GetBySessionIdAsync(mine.JobId, CancellationToken.None))!
            .ThreadId.Should().Be(Dialog, "and nothing of the caller's moved either");
    }

    /// <summary>
    /// A conversation blocked on a question is not moved: the approval gate waits where the
    /// question was asked, and the answer is expected on that thread.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_ALiveQuestion_IsStillRefused()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        _pendingQuestions.Set(opened!.JobId, "q-approval", "file these tickets?");

        var result = await Resume(opened.JobId, FreshDialog);

        StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        (await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None))!
            .ThreadId.Should().Be(Dialog);
        _pendingQuestions.TryPeek(opened.JobId, out _).Should().BeTrue(
            "the question still waits where it was asked");
    }

    /// <summary>
    /// A running turn holds the old thread. Moved under it, its reply would find no open
    /// session and never be stored, and its approval would be accepted in the new tab and
    /// then fail to file — after the operator approved.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_ALiveTurn_IsStillRefused()
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
        await entered.Task.OrHang("the dispatched turn enters the runner");

        var before = _hub.Pushes.Count;

        var result = await Resume(opened!.JobId, FreshDialog);

        StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        release.SetResult();
        await Settle(before + 1);
        var row = await _repository.GetBySessionIdAsync(opened.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(Dialog, "the conversation stays where its turn is running");
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!
            .Transcript.Select(turn => turn.Text).Should().Contain(CannedReply,
                "the running turn's reply still finds its session");
    }

    /// <summary>
    /// Opening a past conversation on the dashboard is a resume onto a freshly minted dialog
    /// id. Nothing is open there, so the resume closes nothing — not the conversation that
    /// replaced it on its old tab.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_OntoAFreshDialogId_ClosesNothingElse()
    {
        await SendAsync("/spec");
        var past = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        await ReplaceTheConversationHereAsync();
        var current = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var result = await Resume(past!.JobId, FreshDialog);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        (await _sessions.GetOpenByThreadAsync(Platform, FreshDialog, CancellationToken.None))!
            .JobId.Should().Be(past.JobId);
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!
            .JobId.Should().Be(current!.JobId, "the conversation on the other tab stays open");
    }

    /// <summary>
    /// Found by review, and older than this phase: the resume closed the target thread with a bulk
    /// update the tracked session did not see, then set the session open again — which, to the
    /// change tracker, was no change. So a resume into the thread a session already lived in
    /// silently closed it while answering that it had resumed. The tracker is deliberately NOT
    /// cleared here: clearing it is exactly what hid the bug from the tests beside this one.
    /// </summary>
    [Fact]
    public async Task ResumeRoute_IntoTheThreadItAlreadyLivesIn_LeavesItOpen()
    {
        await SendAsync("/spec");
        var here = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);

        var result = await Resume(here!.JobId, Dialog);

        StatusOf(result).Should().Be(StatusCodes.Status204NoContent);
        _context.ChangeTracker.Clear();
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))
            .Should().NotBeNull("resuming a conversation where it already is must not close it");
    }

    [Fact]
    public async Task ResumeRoute_AClosedConversationOfAnotherPrincipal_IsNotFound()
    {
        await SendAsync("/spec");
        var past = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        await ReplaceTheConversationHereAsync();

        var result = await Resume(past!.JobId, FreshDialog, Intruder);

        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        var row = await _repository.GetBySessionIdAsync(past.JobId, CancellationToken.None);
        row!.ThreadId.Should().Be(Dialog);
        row.IsOpen.Should().BeFalse("a closed conversation is reopened by its owner or by nobody");
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
        await entered.Task.OrHang("the dispatched turn enters the runner");

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

    // 2026-09-20-4b0aa: the page used to post an opening command and then the message, and the
    // route answers before either has run — so the two were independent background tasks with no
    // ordering, and whichever won decided whether the operator's first sentence existed at all.
    // The project now rides on the message and the two acts happen in one task, in that order.
    [Fact]
    public async Task SpecDialogDispatch_AFirstMessageNamingAProject_OpensTheConversationThenRoutesIt()
    {
        var before = _hub.Pushes.Count;

        await Ingest(FirstSentence, Owner, Dialog, "sample");
        await Settle(before + 2);

        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))
            .Should().NotBeNull("the message that named a project opened its own conversation");
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Once,
            "and then ran, in the same task that opened it");
        LastText().Should().Be(CannedReply);
        AllTexts().Should().NotContain(text => text.Contains(NoOpenDialog, StringComparison.Ordinal));
    }

    /// <summary>
    /// The order is what this proves: admitting a message needs an open session for the thread and
    /// returns null when there is none, so a sentence routed before the open is never stored — and
    /// no later read brings it back.
    /// </summary>
    [Fact]
    public async Task SpecDialogDispatch_AFirstMessage_IsStoredInTheTranscriptItOpened()
    {
        var before = _hub.Pushes.Count;

        await Ingest(FirstSentence, Owner, Dialog, "sample");
        await Settle(before + 2);

        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        state!.Transcript.Select(turn => turn.Text).Should()
            .Equal(FirstSentence, CannedReply);
    }

    // The caller that really has nothing to open: a page that lost its project, or something that
    // is not this page at all. The tutorial is still the right answer to it.
    [Fact]
    public async Task SpecDialogDispatch_AMessageWithNoProjectAndNoSession_StillAnswersWithTheTutorial()
    {
        await SendAsync(FirstSentence);

        LastText().Should().Contain(NoOpenDialog);
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))
            .Should().BeNull("nothing was named to open a conversation on");
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Opening over an open session IS the fork, so the resolve must stop at the session it found —
    // otherwise every message after the first would close the conversation it was sent into.
    [Fact]
    public async Task SpecDialogDispatch_AMessageOnAnOpenSession_OpensNothingAndRoutesAsBefore()
    {
        await SendAsync("/spec");
        var opened = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        var before = _hub.Pushes.Count;

        await Ingest(FirstSentence, Owner, Dialog, "sample");
        await Settle(before + 1);

        ForgetTracked();
        var still = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        still!.JobId.Should().Be(opened!.JobId, "the conversation it was sent into is the one it runs in");
        _hub.Pushes.Skip(before).Select(TextOf).Should()
            .ContainSingle("nothing announced a second opening").Which.Should().Be(CannedReply);
    }

    // The command handler already says why — an unknown project, or several to choose between —
    // and the tutorial on top of that would be a second, vaguer answer to the same question.
    [Fact]
    public async Task SpecDialogDispatch_AProjectThatIsNotConfigured_IsRefusedWithAReasonRatherThanOpened()
    {
        var before = _hub.Pushes.Count;

        await Ingest(FirstSentence, Owner, Dialog, "no-such-project");
        await Settle(before + 1);

        LastText().Should().Contain("Unknown project").And.Contain("no-such-project");
        (await StaysAtAsync(before + 1)).Should().BeTrue(
            "the reason is the whole answer — the tutorial does not follow it");
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))
            .Should().BeNull("a project nobody configured opens nothing");
        _turnRunner.Verify(runner => runner.RunTurnAsync(
            It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()), Times.Never);
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

    /// <summary>The real command handler — the one thing that opens a conversation. The router
    /// reaches it through a typed "/spec", the resolver through a first message that named a
    /// project; its own guard is what keeps the second from forking over the first.</summary>
    private SpecDialogCommandHandler Commands(SpecDialogMessenger messenger) =>
        new(_sessions, new SpecDialogScopeResolver(SingleProjectLoader()),
            new SpecDialogReplyComposer(), messenger);

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
            new SpecCommandParser(), _sessions, Commands(messenger),
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

    /// <summary>The resume route, called the way the page calls it: the conversation by its
    /// session id, the tab it is to be moved onto, and the principal asking.</summary>
    private Task<IResult> Resume(string sessionId, string dialogId, string caller = Owner) =>
        SpecDialogResumeEndpoints.ResumeAsync(
            sessionId,
            new SpecDialogResumeEndpoints.SpecDialogResumeRequest(dialogId),
            Principal(caller), _ownership, _resumer, CancellationToken.None);

    /// <summary>
    /// What "/spec new" used to do for these tests — a thread whose conversation has been
    /// replaced, so the one before it is closed and resumable. The fork left with the spellings
    /// nothing but the parser built.
    /// </summary>
    private async Task ReplaceTheConversationHereAsync()
    {
        await _sessions.CloseAsync(Platform, Dialog, CancellationToken.None);
        ForgetTracked();
        await SendAsync("/spec");
        ForgetTracked();
    }

    private static int StatusOf(IResult result) =>
        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode
        ?? throw new InvalidOperationException("the route answered without a status code");

    private Task<IResult> Ingest(
        string text, string caller, string dialogId = Dialog, string? project = null) =>
        SpecDialogEndpoints.IngestAsync(
            new SpecDialogEndpoints.SpecDialogMessageRequest(dialogId, text, project),
            new DefaultHttpContext { RequestServices = _services, User = Principal(caller) });

    // The endpoint dispatches fire-and-forget, so a test waits for the effect it is about
    // to assert on rather than for a task it was never handed.
    private Task Settle(int pushes) => WaitFor(() => _hub.Pushes.Count >= pushes);

    private Task SettleAnswers() => WaitFor(() => _dialogueTransport.Invocations.Count > 0);

    private static Task WaitFor(Func<bool> reached) =>
        TestWaits.UntilAsync(reached, "the dispatched turn produces what it was waited on for");

    private string LastText() => TextOf(_hub.Pushes.Last());

    private IEnumerable<string> AllTexts() => _hub.Pushes.Select(TextOf);

    private static string TextOf(RecordingDialogHub.HubPush push) =>
        push.Args.OfType<SpecDialogChannelMessage>().Single().Text;

    /// <summary>
    /// That the pushes STOP here. One dispatch is one task, so anything else it would say follows
    /// immediately — waiting for a push that must not come is what turns "and nothing else was
    /// said" into an assertion rather than a snapshot taken early.
    /// </summary>
    private Task<bool> StaysAtAsync(int pushes) =>
        TestWaits.StaysAsync(() => _hub.Pushes.Count == pushes);

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
