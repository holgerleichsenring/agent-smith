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

    private const string Draft = "```yaml\nphase: p9999\ngoal: \"widget goal\"\n```";
    private const string ReplyWithDraft =
        "Here is the phase.\n\n" + Draft + "\n\nApprove it when it reads right.";

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
        _context.Database.Migrate();
        var repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
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

        DashboardTexts().Last().Should().Be("Here is the phase.\n\nApprove it when it reads right.");
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
        Replies((ReplyWithDraft, new PhaseOutcome(new PhaseDraft("p9999", "widget goal", Draft, []))),
            ("A smaller cut.", new AnswerOutcome()));
        _transport.Setup(transport => transport.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string questionId, TimeSpan _, CancellationToken _) =>
                new DialogAnswer(questionId, "cut it smaller", null, DateTimeOffset.UtcNow, "U1"));

        await TurnAsync(Dashboard);

        _turns.Should().HaveCount(2, "the edit note re-runs the design turn");
        PromptOf(_turns[1]).Should().Contain(Draft,
            "the master revises a draft it can still read in its own transcript");
    }

    [Fact]
    public async Task QuestionPump_AQuestionQuotingYaml_IsSentUnchanged()
    {
        const string question = "Should the draft keep this block?\n" + Draft;
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.SubscribeToJobAsync("s-1", It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => OneQuestion(question, ct));
        var pump = new SpecDialogQuestionPump(bus.Object, _messenger, new SpecDialogPendingQuestions(),
            new SpecDialogReplyComposer(), NullLogger<SpecDialogQuestionPump>.Instance);
        using var stop = new CancellationTokenSource();

        var pumping = pump.PumpAsync(State(), stop.Token);
        await WaitForAsync(() => DashboardTexts().Any());
        await stop.CancelAsync();
        await pumping;

        DashboardTexts().Single().Should().Contain(Draft, "a question is not a reply and keeps its fences");
    }

    private async Task TurnAsync(string platform)
    {
        await _sessions.OpenAsync(platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        await _router.TryRouteAsync("draft the phase", "U1", "C1", Thread, platform, CancellationToken.None);
    }

    private void Replies(params (string Reply, OutcomeProposal Outcome)[] replies)
    {
        var queue = new Queue<(string Reply, OutcomeProposal Outcome)>(replies);
        _turnRunner.Setup(runner => runner.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationState state, CancellationToken _) =>
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
                     new Dictionary<string, ISandbox>(), new SpecDialogReplySlot()))
            context.Set(key, value);
        return new SpecDialogPromptFactory().Build(context);
    }

    private static async IAsyncEnumerable<BusMessage> OneQuestion(
        string text, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new BusMessage { Type = BusMessageType.Question, JobId = "s-1", QuestionId = "q-1", Text = text };
        await Task.Delay(Timeout.Infinite, ct);
    }

    private IEnumerable<string> DashboardTexts() =>
        _hub.Pushes.SelectMany(push => push.Args.OfType<SpecDialogChannelMessage>()).Select(m => m.Text);

    private static async Task WaitForAsync(Func<bool> reached)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!reached() && DateTimeOffset.UtcNow < deadline) await Task.Delay(10);
        reached().Should().BeTrue();
    }

    private SpecDialogRouter Router(SpecDialogSessionRepository repository)
    {
        var composer = new SpecDialogOutcomeComposer();
        var pending = new SpecDialogPendingQuestions();
        var gate = new SpecDialogTurnGate();
        var flow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(_transport.Object, _messenger, pending, composer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), composer, _messenger,
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance, _hub),
            new SpecDialogLatestOutcomeStore(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance), NullLogger<SpecDialogOutcomeFlow>.Instance);
        return new SpecDialogRouter(
            new SpecCommandParser(), _sessions,
            new SpecDialogCommandHandler(_sessions,
                new SpecDialogResumer(repository, gate, pending, TimeProvider.System,
                    NullLogger<SpecDialogResumer>.Instance),
                new SpecDialogScopeResolver(Mock.Of<IConfigurationLoader>()),
                new SpecDialogReplyComposer(), _messenger),
            _turnRunner.Object, flow, gate, pending, _transport.Object,
            new SpecDialogReplyComposer(), _messenger, NullLogger<SpecDialogRouter>.Instance);
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
