using AgentSmith.Application.Services.SpecDialog;
using System.Security.Claims;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: what the dashboard's dialog surface is served — framework lines
/// composed for a browser, the master's own reply left alone, a typed question that says
/// what kind it is, and the scope the conversation is grounded in.
/// </summary>
public sealed class DashboardDialogSurfaceTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-cb3e";
    private const string Owner = "person-a";
    private const string Intruder = "person-b";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SpecDialogOwnership _ownership;
    private readonly RecordingDialogHub _hub = new();

    public DashboardDialogSurfaceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        _ownership = new SpecDialogOwnership(_repository, new SpecCommandParser());
        _turns = new SpecDialogTurnGate(_clock);
        // The same gate: setting and taking a question is what stops and starts the turn's clock.
        _pending = new SpecDialogPendingQuestions(_turns);
    }

    [Fact]
    public void Composer_ForTheBrowserChannel_EmitsCommonMark()
    {
        var composer = new SpecDialogReplyComposer();

        var opened = composer.ComposeOpened(State()).In(SpecDialogMarkup.CommonMark);
        var question = composer.ComposeQuestion("Which repository holds the ledger?")
            .In(SpecDialogMarkup.CommonMark);

        opened.Should().Contain("**sample**", "a browser reads double asterisks as bold");
        question.Should().NotContain(":question:",
            "a shortcode nobody expands is the text of another channel showing through");
        // 2026-09-17-042ek: the dialect still binds the italics; the sentence inside them is
        // the page's, because the page has no thread to reply in.
        question.Should().Contain("*Write your answer below.*");
    }

    [Fact]
    public void Composer_ForAChatChannel_EmitsMrkdwn()
    {
        var composer = new SpecDialogReplyComposer();

        var opened = composer.ComposeOpened(State()).In(SpecDialogMarkup.ChatMrkdwn);
        var question = composer.ComposeQuestion("Which repository?").In(SpecDialogMarkup.ChatMrkdwn);

        opened.Should().Contain("*sample*").And.NotContain("**sample**");
        question.Should().StartWith(":question:");
    }

    [Fact]
    public async Task Messenger_TheMastersOwnReply_IsSentByteForByte()
    {
        // What a design master writes: ordinary markdown, including the fences and the
        // single-asterisk italics a chat converter would have eaten.
        const string reply =
            "**The ledger** is *one* table.\n\n```sql\nSELECT 1;\n```\n_Not_ two.";

        await Messenger().SendAsync(Platform, Dialog, Dialog, reply, CancellationToken.None);

        LastText().Should().Be(reply);
    }

    [Fact]
    public async Task Messenger_AComposedLine_IsBoundToTheChannelItIsGoingTo()
    {
        var composed = new SpecDialogReplyComposer().ComposeOpened(State());

        await Messenger().SendAsync(Platform, Dialog, Dialog, composed, CancellationToken.None);

        LastText().Should().Contain("**sample**").And.NotContain(":question:");
    }

    [Fact]
    public async Task AskTypedQuestion_TheOutcomeGate_CarriesItsKindThoughItCarriesNoChoices()
    {
        // The confirmation gate asks an Approval question with Choices: null — every other
        // adapter builds the approve/reject pair from the TYPE.
        var question = new DialogQuestion(
            "q-1", QuestionType.Approval, "File these two tickets?",
            Context: null, Choices: null, DefaultAnswer: "", TimeSpan.FromMinutes(15));

        await Adapter().AskTypedQuestionAsync(Dialog, question, Dialog, CancellationToken.None);

        var pushed = LastQuestion();
        pushed.Kind.Should().Be("approval");
        pushed.Choices.Should().BeEmpty();
    }

    [Fact]
    public async Task AskTypedQuestion_AChoiceQuestion_CarriesTheChoicesItWasAskedWith()
    {
        var question = new DialogQuestion(
            "q-2", QuestionType.Choice, "Which slice first?", Context: null,
            Choices: [new DialogChoice("the reader"), new DialogChoice("the writer")],
            DefaultAnswer: "", TimeSpan.FromMinutes(15));

        await Adapter().AskTypedQuestionAsync(Dialog, question, Dialog, CancellationToken.None);

        var pushed = LastQuestion();
        pushed.Kind.Should().Be("choice");
        pushed.Choices.Select(choice => choice.Label).Should().Equal("the reader", "the writer");
    }

    [Fact]
    public async Task ReadAsync_AnOpenSession_CarriesItsScopeAndItsTranscript()
    {
        await OpenAsync(Owner);
        await _sessions.AppendTurnAsync(
            Platform, Dialog, TranscriptRole.User, "a widget that reads the ledger",
            null, null, CancellationToken.None);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Session.Should().NotBeNull();
        view.Session!.Scope.Name.Should().Be("sample");
        view.Session.Scope.Repos.Should().Equal("repo-a");
        view.Session.Scope.Templates.Should().ContainSingle()
            .Which.Name.Should().Be("template:default");
        view.Session.Transcript.Should().ContainSingle()
            .Which.Role.Should().Be("user");
    }

    [Fact]
    public async Task ReadAsync_WithNoSessionOpen_CarriesTheProjectsOneCouldBeOpenedOn()
    {
        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Session.Should().BeNull();
        view.Projects.Should().ContainSingle().Which.Templates.Should().ContainSingle()
            .Which.Revision.Should().Be("v1.2");
    }

    [Fact]
    public async Task ReadAsync_AForeignDialogId_IsRefused()
    {
        await OpenAsync(Owner);

        var result = await SpecDialogViewEndpoints.ReadAsync(
            Dialog, Principal(Intruder), _ownership, Reader(), CancellationToken.None);

        result.Should().BeOfType<StatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// The approval that files tickets reaches the page as a hub push and lives nowhere
    /// else. Reloading during the fifteen-minute gate left a blocked master and a page with
    /// no question and no button; the read has to rebuild it.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ATurnBlockedOnAQuestion_CarriesItSoAReloadKeepsTheGate()
    {
        await OpenAsync(Owner);
        var state = await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(15);
        _pending.Set(state!.JobId, Approval("file these three tickets?"), deadline);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Question.Should().NotBeNull();
        view.Question!.Text.Should().Be("file these three tickets?");
        view.Question.Kind.Should().Be("approval");
        view.Question.ExpiresAt.Should().BeCloseTo(deadline, TimeSpan.FromSeconds(1),
            "without it the card offers a button after the wait has gone");
    }

    [Fact]
    public async Task ReadAsync_ATurnBlockedOnNothing_CarriesNoQuestion()
    {
        await OpenAsync(Owner);

        (await Reader().ReadAsync(Dialog, CancellationToken.None))
            .Question.Should().BeNull();
    }

    // 2026-09-18-2f8b: a page that did NOT post the message — one opened on a second screen,
    // or reloaded mid-turn — issues this read and nothing else until the reply lands. Without
    // the turn on it, that page shows a conversation that looks finished while it is running.
    [Fact]
    public async Task View_ComputingTurn_IsOnTheViewForAPageThatDidNotPostIt()
    {
        await OpenAsync(Owner);
        var turn = _turns.Begin(await SessionIdAsync());
        turn.Keep(new SpecDialogActivityPush(
            Dialog, "tool", "read_file", "repo-a/src/Router.cs", _clock.Now, turn.Next(),
            turn.StartedAt));

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Turn.Computing.Should().BeTrue();
        view.Turn.Steps.Should().ContainSingle().Which.Seq.Should().Be(1,
            "the page merges the pushes that follow against this number");
    }

    // The design turn's own ask_human blocks INSIDE the turn's execution, and that wait has no
    // deadline at all — it ends when the person answers. Reported as computing, it would render
    // a working line beside the very card asking them to answer.
    [Fact]
    public async Task View_TurnBlockedOnAQuestion_IsNotComputing()
    {
        await OpenAsync(Owner);
        var sessionId = await SessionIdAsync();
        _turns.Begin(sessionId);
        // The turn's OWN ask_human, as the question pump sets it: free text, no deadline at
        // all, because nothing ends that wait but the person.
        _pending.Set(sessionId, "q-1", "which repository holds the ledger?");

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Question.Should().NotBeNull("the card is what the person is waiting on");
        view.Question!.ExpiresAt.Should().BeNull("nothing ends an ask_human wait but an answer");
        view.Turn.Computing.Should().BeFalse();
        view.Turn.ElapsedSeconds.Should().Be(0);
    }

    // The wait on a person is not computation. Measured from the start instant, a turn that
    // computed forty seconds and then waited twenty minutes for an answer would be shown to a
    // colleague on a second screen as having worked for twenty minutes and forty seconds —
    // the same untruth the flag above exists to prevent, on the duration axis.
    [Fact]
    public async Task View_TimeSpentWaitingForAnAnswer_IsNotCountedAsComputing()
    {
        await OpenAsync(Owner);
        var sessionId = await SessionIdAsync();
        _turns.Begin(sessionId);
        _clock.Now = _clock.Now.AddSeconds(40);
        _pending.Set(sessionId, "q-1", "which repository holds the ledger?");
        _clock.Now = _clock.Now.AddMinutes(20);
        _pending.TryTake(sessionId, Peeked(sessionId)).Should().BeTrue();
        _clock.Now = _clock.Now.AddSeconds(5);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Turn.Computing.Should().BeTrue();
        view.Turn.ElapsedSeconds.Should().Be(45,
            "forty seconds before the question and five after the answer; the wait is not work");
    }

    // The sequence restarts every turn, so a page that missed the reply between two turns
    // would read the new turn's steps as duplicates of the dead turn's and discard them.
    [Fact]
    public async Task View_ComputingTurn_NamesTheTurnItsStepsBelongTo()
    {
        await OpenAsync(Owner);
        var sessionId = await SessionIdAsync();
        var first = _turns.Begin(sessionId);
        _turns.Finish(sessionId, first);
        _clock.Now = _clock.Now.AddSeconds(30);
        var second = _turns.Begin(sessionId);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Turn.TurnStartedAt.Should().Be(second.StartedAt).And.NotBe(first.StartedAt);
    }

    // Finish is handed the turn it is ending. Removing whatever sits under the key would let a
    // slow turn's teardown end the turn that had already replaced it.
    [Fact]
    public async Task Gate_FinishingASupersededTurn_LeavesTheCurrentOneComputing()
    {
        await OpenAsync(Owner);
        var sessionId = await SessionIdAsync();
        var first = _turns.Begin(sessionId);
        var second = _turns.Begin(sessionId);

        _turns.Finish(sessionId, first);

        _turns.Liveness(sessionId).TurnStartedAt.Should().Be(second.StartedAt);
    }

    private PendingQuestion Peeked(string sessionId)
    {
        _pending.TryPeek(sessionId, out var peeked).Should().BeTrue();
        return peeked;
    }

    // 2026-09-18-2f8b reverses 2026-09-17-c7aed's "no duration, because nothing measures one":
    // the gate measures one now. Read off the server so two clocks are never differenced.
    [Fact]
    public async Task View_ComputingTurn_CarriesElapsedSecondsFromTheInjectedTimeSource()
    {
        await OpenAsync(Owner);
        _turns.Begin(await SessionIdAsync());
        _clock.Now = _clock.Now.AddSeconds(42);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Turn.ElapsedSeconds.Should().Be(42);
    }

    [Fact]
    public async Task View_NoTurn_ReadsAsNotComputingWithNoSteps()
    {
        await OpenAsync(Owner);

        var view = await Reader().ReadAsync(Dialog, CancellationToken.None);

        view.Turn.Computing.Should().BeFalse();
        view.Turn.ElapsedSeconds.Should().Be(0);
        view.Turn.Steps.Should().BeEmpty();
    }

    private static DialogQuestion Approval(string text) =>
        new(Guid.NewGuid().ToString("N"), QuestionType.Approval, text,
            Context: null, Choices: null, DefaultAnswer: "", TimeSpan.FromMinutes(15));

    private Task OpenAsync(string owner, string dialogId = Dialog) =>
        _sessions.OpenAsync(Platform, dialogId, dialogId, owner,
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);

    private readonly SpecDialogPendingQuestions _pending;

    private SpecDialogViewReader Reader() =>
        new(_sessions, new SpecDialogProjectCatalog(Loader()), _pending,
            new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance),
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()), _turns);

    /// <summary>A clock the test moves by hand, so no assertion about elapsed seconds waits
    /// on a real one.</summary>
    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly StoppedClock _clock = new(new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero));
    private readonly SpecDialogTurnGate _turns;

    private async Task<string> SessionIdAsync() =>
        (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!.JobId;

    private SpecDialogMessenger Messenger() =>
        new([Adapter()], NullLogger<SpecDialogMessenger>.Instance);

    private DashboardAdapter Adapter() => new(NullLogger<DashboardAdapter>.Instance, _hub);

    private string LastText() => _hub.Pushes.Last().Args
        .OfType<SpecDialogChannelMessage>().Single().Text;

    private SpecDialogChannelQuestion LastQuestion() => _hub.Pushes.Last().Args
        .OfType<SpecDialogChannelQuestion>().Single();

    private static ConversationState State() => new()
    {
        JobId = "s-1", ChannelId = Dialog, UserId = Owner, Platform = Platform,
        Project = "sample", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        Scope = new ActiveScope { Project = "sample", Repos = ["repo-a"] },
    };

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "test"));

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new()
                {
                    Name = "sample",
                    Repos = [new RepoConnection { Name = "repo-a" }],
                    Templates =
                    [
                        new ProjectTemplate("default", "house", "v1.2",
                            new RepoConnection { Name = "template-repo" }),
                    ],
                },
            },
        });
        return loader.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
