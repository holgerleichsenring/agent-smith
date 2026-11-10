using AgentSmith.Tests.TestSupport;
using System.Runtime.CompilerServices;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// A reply that proposes work is remembered whole and shown per platform: the transcript is
/// the design master's only memory, the dashboard shows the draft in its pane instead of in
/// the prose, and a chat thread — which has no pane — reads the reply unchanged. The REAL
/// router over the REAL durable store; only the design turn is scripted, and its result is
/// built by the same factory the turn runner uses.
/// </summary>
public sealed class DialogDraftSplitTests : IDisposable
{
    private const string Dashboard = "dashboard";
    private const string Slack = "slack";
    private const string Thread = "d-c7aea";

    private const string Draft = "```yaml\nspec: p9999\ngoal: \"widget goal\"\n```";
    private const string ReplyWithDraft =
        "Here is the spec.\n\n" + Draft + "\n\nApprove it when it reads right.";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionManager _sessions;
    private readonly RecordingDialogHub _hub = new();
    private readonly Mock<IPlatformAdapter> _slack = new();
    private readonly Mock<ISpecDialogTurnRunner> _turnRunner = new();
    private readonly Mock<IDialogueTransport> _transport = new();
    private readonly List<ConversationState> _turns = [];
    private readonly SpecDialogMessenger _messenger;
    private readonly SpecDialogRouter _router;

    public DialogDraftSplitTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
        var repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(repository), AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        _slack.SetupGet(adapter => adapter.Platform).Returns(Slack);
        _messenger = new SpecDialogMessenger(
            [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, _hub), _slack.Object],
            NullLogger<SpecDialogMessenger>.Instance);
        Replies((ReplyWithDraft, new AnswerOutcome()));
        _router = Router(repository);
    }

    [Fact]
    public async Task Router_OnTheDashboard_SendsTheReplyWithoutItsDraft()
    {
        await TurnAsync(Dashboard);

        DashboardTexts().Last().Should().Be("Here is the spec.\n\nApprove it when it reads right.");
    }

    [Fact]
    public async Task Router_OnSlack_SendsTheReplyUnchanged()
    {
        await TurnAsync(Slack);

        _slack.Verify(adapter => adapter.SendInfoAsync(
            "C1", It.IsAny<string>(), ReplyWithDraft, Thread, It.IsAny<CancellationToken>()),
            Times.Once, "a chat thread has no pane; the reply is where its draft is read");
    }

    [Fact]
    public async Task Transcript_AfterAReplyWithADraft_StillHoldsTheDraft()
    {
        await TurnAsync(Dashboard);

        var state = await _sessions.GetOpenByThreadAsync(Dashboard, Thread, CancellationToken.None);
        state!.Transcript.Where(turn => turn.Role == TranscriptRole.Assistant)
            .Select(turn => turn.Text).Should().Equal(ReplyWithDraft);
    }

    [Fact]
    public async Task EditNote_ReRun_TheMasterStillSeesItsDraft()
    {
        Replies((ReplyWithDraft, new SpecOutcome(new SpecDraft("p9999", "widget goal", Draft, []))),
            ("A smaller cut.", new AnswerOutcome()));

        await TurnAsync(Dashboard);
        // 2026-11-10-ccd9: the note is an ordinary message, and it runs an ordinary turn.
        await _router.TryRouteAsync("cut it smaller", "U1", "C1", Thread, Dashboard, false, CancellationToken.None);

        _turns.Should().HaveCount(2, "the note runs the design turn again");
        PromptOf(_turns[1]).Should().Contain(Draft,
            "the master revises a draft it can still read in its own transcript");
    }

    [Fact]
    public async Task Turn_AfterAFiledProposal_IsShownNoFindings()
    {
        var reviewed = new SpecOutcome(new SpecDraft("p9999", "widget goal", Draft, [])) with
        {
            Findings = [new ProposalFinding("p9999", "false premise", "already there")],
        };
        Replies((ReplyWithDraft, reviewed));

        await TurnAsync(Dashboard);
        var id = (await new SpecDialogLatestOutcomeStore(new SpecDialogSessionRepository(_context),
                AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(new SpecDialogSessionRepository(_context)),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpecDialogLatestOutcomeStore>.Instance)
            .ReadAsync(Dashboard, Thread, CancellationToken.None)).ProposalId;
        await _router.TryRouteAsync("approve", "U1", "C1", Thread, Dashboard, false, CancellationToken.None, proposalId: id);
        await _router.TryRouteAsync("now the next thing", "U1", "C1", Thread, Dashboard, false, CancellationToken.None);

        _turns.Should().HaveCount(2, "the approved proposal was filed; this is an ordinary next turn");
        PromptOf(_turns[1]).Should().NotContain("the review of your last proposal",
            "a stored proposal is cleared only on reject or timeout — reading it back would be stale");
    }

    [Fact]
    public async Task QuestionPump_AQuestionQuotingYaml_IsSentUnchanged()
    {
        const string question = "Should the draft keep this block?\n" + Draft;
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.TailAsync("s-1", It.IsAny<CancellationToken>())).ReturnsAsync("0-0");
        bus.Setup(b => b.SubscribeToJobAsync("s-1", "0-0", It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken ct) => OneQuestion(question, ct));
        var pump = new SpecDialogQuestionPump(bus.Object, _messenger, new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
            new SpecDialogReplyComposer(), NullLogger<SpecDialogQuestionPump>.Instance);
        using var stop = new CancellationTokenSource();

        var pumping = await pump.StartAsync(State(), stop.Token);
        await WaitForAsync(() => DashboardTexts().Any());
        await stop.CancelAsync();
        await pumping;

        DashboardTexts().Single().Should().Contain(Draft, "a question is not a reply and keeps its fences");
    }

    private async Task TurnAsync(string platform)
    {
        await _sessions.OpenAsync(platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        await _router.TryRouteAsync("draft the spec", "U1", "C1", Thread, platform, false, CancellationToken.None);
    }

    private void Replies(params (string Reply, OutcomeProposal Outcome)[] replies)
    {
        var queue = new Queue<(string Reply, OutcomeProposal Outcome)>(replies);
        _turnRunner.Setup(runner => runner.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState state, TurnPoster ___, CancellationToken _) =>
            {
                _turns.Add(state);
                var (reply, outcome) = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
                return SpecDialogTurnResult.On(state.Platform, reply, outcome);
            });
    }

    private static string PromptOf(ConversationState state)
    {
        var context = new PipelineContext();
        foreach (var (key, value) in SpecDialogTurnSeeds.Build(
                     state, [new RepoConnection { Name = "repo-a" }],
                     new Dictionary<string, ISandbox>(), new SpecDialogReplySlot(),
                     DialogImageSet.None, new SpecDialogTurnFacts(Mock.Of<IFiledTicketWithdrawal>())))
            context.Set(key, value);
        return new SpecDialogPromptFactory().Build(context, 0, 0);
    }

    private static async IAsyncEnumerable<BusMessage> OneQuestion(
        string text, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new BusMessage { Type = BusMessageType.Question, JobId = "s-1", QuestionId = "q-1", Text = text };
        await Task.Delay(Timeout.Infinite, ct);
    }

    private IEnumerable<string> DashboardTexts() =>
        _hub.Pushes.SelectMany(push => push.Args.OfType<SpecDialogChannelMessage>()).Select(m => m.Text);

    private static Task WaitForAsync(Func<bool> reached) =>
        TestWaits.UntilAsync(reached, "the dispatched turn produces what it was waited on for");

    private SpecDialogRouter Router(SpecDialogSessionRepository repository)
    {
        var composer = new SpecDialogOutcomeComposer();
        var pending = new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System));
        var gate = new SpecDialogTurnGate(TimeProvider.System);
        var flowHarness = new AgentSmith.Tests.TestSupport.SpecDialogDecisionHarness(_context, new DashboardOutcomeChannel(new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()), NullLogger<DashboardOutcomeChannel>.Instance, _hub), Mock.Of<IOutcomeSink>(), _messenger);
        var flow = flowHarness.Flow();
        return flowHarness.Router(new SpecDialogCommandHandler(_sessions,
                new SpecDialogScopeResolver(Mock.Of<IConfigurationLoader>()),
                new SpecDialogReplyComposer(), _messenger), _turnRunner.Object, gate, new SpecDialogAnswerAdmission(_sessions, pending, _transport.Object), SilentTurnKeeper.Over(repository, _sessions, _context));
    }

    private static ConversationState State() => new()
    {
        JobId = "s-1", ChannelId = Thread, ThreadId = Thread, UserId = "U1", Platform = Dashboard,
        Project = "sample", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
    };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
