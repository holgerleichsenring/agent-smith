using System.Text.Json.Nodes;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Server;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-26-0f19b: a spec dialog's approval buttons are answered by whichever replica the chat
/// platform delivers the click to. Replica A asks and waits; replica B, which never asked, receives
/// the click. They share one database, one dialogue stream and one Redis, as two servers do.
/// </summary>
public sealed class CrossReplicaButtonAnswerTests : IDisposable
{
    private const string Thread = "1700.01";
    private readonly ChatRunHarness _store = new();
    private readonly SharedDialogueStream _stream = new();
    private readonly FakeRedisStreams _redis = new();
    private readonly RecordingHttpHandler _replicaBSlack = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task SlackClick_OnAReplicaThatDidNotAsk_AnswersTheWaitingConfirmation()
    {
        var state = await OpenSessionAsync("slack", Thread);
        var (confirmation, questionId) = await AskOnReplicaAAsync(state);

        await SlackHandlerOnReplicaB().HandleAsync("C1", questionId, "approve", Click(), default);

        (await confirmation.OrHang("the confirmation")).Should().BeOfType<OutcomeConfirmed>();
        _replicaBSlack.Requests.Should().Contain(r => r.Url.EndsWith("chat.update"), "the buttons give way to the answer");
    }

    [Fact]
    public async Task TeamsCard_OnAReplicaThatDidNotAsk_AnswersTheWaitingConfirmation()
    {
        var state = await OpenSessionAsync("teams", "19:conv");
        var (confirmation, questionId) = await AskOnReplicaAAsync(state);
        var value = JsonNode.Parse($$$"""{"questionId":"{{{questionId}}}","answer":"approve"}""")!;

        await new TeamsInteractionHandler(_store.Get<ChatRunAnswerRouter>(), null!, null!,
                new TeamsAdapter(null!, new TeamsTypedQuestionTracker(), null!, NullLogger<TeamsAdapter>.Instance),
                Buttons(), NullLogger<TeamsInteractionHandler>.Instance)
            .HandleAsync("19:conv", "A9", value, default);

        (await confirmation.OrHang("the confirmation")).Should().BeOfType<OutcomeConfirmed>();
    }

    [Fact]
    public async Task SlackClick_OnAClosedQuestion_AnswersNothing()
    {
        var state = await OpenSessionAsync("slack", Thread);

        await SlackHandlerOnReplicaB().HandleAsync("C1", "q-expired", "approve", Click(), default);

        _stream.Answers.Should().BeEmpty("no confirmation waits on that question any more");
        _replicaBSlack.Requests.Should().NotContain(r => r.Url.EndsWith("chat.update"), "nothing was answered");
        state.JobId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RedisOpenDialogQuestions_OpenThenClose_IsOpenOnlyBetween()
    {
        var open = OpenQuestions();
        await open.OpenAsync("sess-1", "q-1", TimeSpan.FromMinutes(15), default);

        (await open.IsOpenAsync("sess-1", "q-1", default)).Should().BeTrue();
        (await open.IsOpenAsync("sess-1", "q-0", default)).Should().BeFalse("only the question the session waits on");
        await open.CloseAsync("sess-1", "q-0", default);
        (await open.IsOpenAsync("sess-1", "q-1", default)).Should().BeTrue("closing another question leaves it open");
        await open.CloseAsync("sess-1", "q-1", default);
        (await open.IsOpenAsync("sess-1", "q-1", default)).Should().BeFalse();
    }

    private async Task<ConversationState> OpenSessionAsync(string platform, string thread) =>
        await SpecDialogButtons.Sessions(_store.Connection).OpenAsync(platform, "C1", thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);

    private async Task<(Task<ConfirmationResult> Confirmation, string QuestionId)> AskOnReplicaAAsync(
        ConversationState state)
    {
        var pending = new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System));
        var confirmer = new SpecDialogOutcomeConfirmer(
            _stream,
            new SpecDialogMessenger([SlackAdapter(new RecordingHttpHandler())], NullLogger<SpecDialogMessenger>.Instance),
            pending, new SpecDialogOutcomeComposer(), UnreviewedApprovals.Unused(), OpenQuestions(),
            NullLogger<SpecDialogOutcomeConfirmer>.Instance);
        var confirmation = confirmer.ConfirmAsync(
            state, new SpecOutcome(new SpecDraft("p9001", "goal", "spec: p9001", [])), CancellationToken.None);
        await TestWaits.UntilAsync(() => pending.TryPeek(state.JobId, out _), "replica A asked");
        pending.TryPeek(state.JobId, out var asked);
        (await TestWaits.ReachedAsync(() => OpenQuestions().IsOpenAsync(state.JobId, asked.Question.QuestionId, default)))
            .Should().BeTrue("replica A opened its question");
        return (confirmation, asked.Question.QuestionId);
    }

    private SlackInteractionHandler SlackHandlerOnReplicaB() => new(
        _store.Get<ChatRunAnswerRouter>(), null!, null!, SlackAdapter(_replicaBSlack), Buttons(),
        NullLogger<SlackInteractionHandler>.Instance);

    private SpecDialogButtonAnswer Buttons() => SpecDialogButtons.Over(_store.Connection, _stream, OpenQuestions());

    private RedisOpenDialogQuestions OpenQuestions() =>
        new(_redis.Connection, NullLogger<RedisOpenDialogQuestions>.Instance);

    private static JsonNode Click() => JsonNode.Parse(
        $$$"""{"user":{"id":"U9"},"message":{"ts":"1700.09","thread_ts":"{{{Thread}}}"}}""")!;

    private static SlackAdapter SlackAdapter(RecordingHttpHandler api) => new(
        new SlackApiClient(new HttpClient(api), new SlackAdapterOptions { BotToken = "t" },
            NullLogger<SlackApiClient>.Instance),
        new SlackTypedQuestionBlockBuilder(), new SlackMessageBlockBuilder(), NullLogger<SlackAdapter>.Instance);

    /// <summary>One dialogue stream both replicas read and write, as Redis is; a wait ends when its answer lands.</summary>
    private sealed class SharedDialogueStream : IDialogueTransport
    {
        private readonly Dictionary<(string, string), TaskCompletionSource<DialogAnswer?>> _waits = [];

        public List<(string JobId, DialogAnswer Answer)> Answers { get; } = [];

        public Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken ct) => Task.CompletedTask;

        public Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken ct)
        {
            lock (_waits)
            {
                Answers.Add((jobId, answer));
                Wait(jobId, answer.QuestionId).TrySetResult(answer);
            }
            return Task.CompletedTask;
        }

        public Task<DialogAnswer?> WaitForAnswerAsync(string jobId, string questionId, TimeSpan timeout, CancellationToken ct)
        {
            lock (_waits) return Wait(jobId, questionId).Task.WaitAsync(ct);
        }

        private TaskCompletionSource<DialogAnswer?> Wait(string jobId, string questionId)
        {
            if (!_waits.TryGetValue((jobId, questionId), out var wait))
                _waits[(jobId, questionId)] = wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return wait;
        }
    }
}
