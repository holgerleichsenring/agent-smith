using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.ChatRuns;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a Slack button on a conversation's card is the clicker's own message carrying the card's id. An
/// approve or reject decides that card without waiting on the turn gate, and only a decision marks the card answered;
/// a shape runs an ordinary turn; a click no conversation card owns is offered to the thread's run first.
/// </summary>
public sealed class SpecDialogChatClickTests : IDisposable
{
    private const string Thread = "1700.01";

    private readonly ChatRunHarness _chat = new();
    private readonly RecordingHttpHandler _slackApi = new();
    private readonly AgentSmithDbContext _context;
    private readonly Mock<IOutcomeSink> _sink = new();
    private readonly Mock<ISpecDialogTurnRunner> _turns = new();
    private readonly SpecDialogTurnGate _gate = new(TimeProvider.System);
    private readonly SpecDialogDecisionHarness _harness;

    public SpecDialogChatClickTests()
    {
        _context = MigratedStoreTemplate.Context(_chat.Connection);
        _harness = new SpecDialogDecisionHarness(_context, sink: _sink.Object, threads: [_chat.Slack]);
        _turns.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On("slack", "Split into two.", new AnswerOutcome()));
    }

    public void Dispose()
    {
        _context.Dispose();
        _chat.Dispose();
    }

    [Fact]
    public async Task SlackInteraction_DecidesWithMayStartRunsFalse()
    {
        var id = await ProposedAsync("p1");

        await ClickAsync(id, "approve");

        _sink.Verify(s => s.AcceptAsync(It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), false, It.IsAny<CancellationToken>()),
            Times.Once, "a chat click carries no runs.control");
        Updated().Should().Be(1, "a decided card gives its buttons up");
    }

    // M2: a click that names its card is one compare-and-set; a running turn does not swallow it.
    [Fact]
    public async Task SlackApprove_WhileATurnRuns_DecidesWithoutTheGate()
    {
        var id = await ProposedAsync("p1");
        var state = (await _harness.Sessions.GetOpenByThreadAsync("slack", Thread, default))!;
        _gate.TryEnter(state.JobId);

        await ClickAsync(id, "approve");

        Filed().Should().Be(1);
        Updated().Should().Be(1);
    }

    [Fact]
    public async Task SlackShapeButton_RoutesAsTheClickersMessageWithTheId()
    {
        var id = await ProposedAsync("p1");

        await ClickAsync(id, "split it");

        _turns.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()), Times.Once);
        var said = (await _harness.Sessions.GetOpenByThreadAsync("slack", Thread, default))!.Transcript
            .Last(t => t.Role == TranscriptRole.User);
        said.Text.Should().Be("split it");
        Filed().Should().Be(0);
        Updated().Should().Be(0, "a shape decides nothing, so the card keeps its buttons");
    }

    [Fact]
    public async Task OldRandomQuestionId_TriesRunAnswerFirstThenNotice()
    {
        await ChatRunBindingTests.StartAsync(_chat, new ChatThread("slack", "C1", Thread, "U1"), "run-1");
        _chat.SeedRun("run-1", "running", finished: false);
        _chat.LiveQuestions.Latest["run-1"] = new DialogQuestion(
            "q-run", QuestionType.Approval, "Ship?", null, null, null, TimeSpan.FromHours(1));
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(default);
        var id = await ProposedAsync("p1");

        await ClickAsync("q-run", "approve");
        (await _chat.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-run", default))!.Answer.Should().Be("approve");
        (await NoticesAsync()).Should().BeEmpty("the run took the click first");

        await ClickAsync("q-gone", "approve");
        (await NoticesAsync()).Should().ContainSingle().Which.Should().Contain("replaced");
        _chat.Slack.Questions.Select(q => q.Question.QuestionId).Where(q => q != "q-run").Should().Equal(id, id);
        Filed().Should().Be(0);
        Updated().Should().Be(1, "only the run's answered question gives its buttons up");
    }

    private async Task<string> ProposedAsync(string specId)
    {
        var state = await _harness.OpenAsync("slack", Thread);
        await _harness.Flow().HandleAsync(state, new SpecOutcome(new SpecDraft(specId, $"goal of {specId}", $"spec: {specId}", [])), default);
        return (await _harness.Outcomes.ReadAsync("slack", Thread, default)).ProposalId!;
    }

    private Task ClickAsync(string questionId, string answer) => Handler().HandleAsync("C1", questionId, answer,
        JsonNode.Parse($$"""{"user":{"id":"U9"},"message":{"ts":"1700.09","thread_ts":"{{Thread}}"},"actions":[{"block_id":"{{questionId}}"}]}""")!,
        default);

    private SlackInteractionHandler Handler()
    {
        var repository = new SpecDialogSessionRepository(_context);
        var sessions = _harness.Sessions;
        var router = _harness.Router(null!, _turns.Object, _gate,
            new SpecDialogAnswerAdmission(sessions, new SpecDialogPendingQuestions(_gate), Mock.Of<IDialogueTransport>()),
            SilentTurnKeeper.Over(repository, sessions, _context));
        var slack = new SlackAdapter(
            new SlackApiClient(new HttpClient(_slackApi), new SlackAdapterOptions { BotToken = "t" }, NullLogger<SlackApiClient>.Instance),
            new SlackTypedQuestionBlockBuilder(), new SlackMessageBlockBuilder(), NullLogger<SlackAdapter>.Instance);
        return new SlackInteractionHandler(_chat.Get<ChatRunAnswerRouter>(), null!, null!, slack,
            new SpecDialogChatClick(sessions, new SpecDialogProposalClaim(_context), router), NullLogger<SlackInteractionHandler>.Instance);
    }

    private int Filed() => _sink.Invocations.Count(i => i.Method.Name == nameof(IOutcomeSink.AcceptAsync));

    private int Updated() => _slackApi.Requests.Count(r => r.Url.EndsWith("chat.update"));

    private async Task<IReadOnlyList<string>> NoticesAsync() =>
        [.. (await _harness.Sessions.GetOpenByThreadAsync("slack", Thread, default))!.Transcript
            .Where(t => t.Kind == SpecDialogTurnKind.Notice).Select(t => t.Text)];
}
