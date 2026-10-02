using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatRuns;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>
/// A button in Slack and a card in Teams answer the question of the run bound to their thread —
/// with no conversation state anywhere, which is what a chat-started run no longer has.
/// </summary>
public sealed class ChatRunInteractionTests : IDisposable
{
    private readonly ChatRunHarness _chat = new();
    private readonly RecordingHttpHandler _slackApi = new();

    public void Dispose() => _chat.Dispose();

    [Fact]
    public async Task SlackButton_InTheBoundThread_AnswersTheRun()
    {
        await BindWithQuestionAsync(new ChatThread("slack", "C1", "1700.01", "U1"), "q-5");
        var payload = JsonNode.Parse(
            """{"user":{"id":"U9"},"message":{"ts":"1700.09","thread_ts":"1700.01"},"actions":[{"block_id":"q-5"}]}""")!;

        await SlackHandler().HandleAsync("C1", "q-5", "yes", payload, default);

        var answer = await _chat.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-5", default);
        answer!.Answer.Should().Be("yes");
        answer.AnsweredBy.Should().Be("U9");
        _slackApi.Requests.Should().Contain(r => r.Url.EndsWith("chat.update"), "the buttons give way to the answer");
    }

    [Fact]
    public async Task SlackButton_InAnotherThread_ReachesNoRun()
    {
        await BindWithQuestionAsync(new ChatThread("slack", "C1", "1700.01", "U1"), "q-5");
        var payload = JsonNode.Parse("""{"user":{"id":"U9"},"message":{"ts":"1800.09","thread_ts":"1800.01"}}""")!;

        await SlackHandler().HandleAsync("C1", "q-5", "yes", payload, default);

        (await _chat.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-5", default)).Should().BeNull();
    }

    [Fact]
    public async Task TeamsCard_InTheBoundConversation_AnswersTheRunWithItsComment()
    {
        await BindWithQuestionAsync(new ChatThread("teams", "19:conv", "19:conv", "A1"), "q-6");
        var value = JsonNode.Parse("""{"questionId":"q-6","answer":"approve","comment":"ship it"}""")!;

        await new TeamsInteractionHandler(
                _chat.Get<ChatRunAnswerRouter>(), null!, null!, TeamsAdapter(),
                NullLogger<TeamsInteractionHandler>.Instance)
            .HandleAsync("19:conv", "A2", value, default);

        var answer = await _chat.Get<IDialogueAnswerInbox>().GetAsync("run-1", "q-6", default);
        answer!.Answer.Should().Be("approve");
        answer.Comment.Should().Be("ship it");
    }

    private async Task BindWithQuestionAsync(ChatThread thread, string questionId)
    {
        await ChatRunBindingTests.StartAsync(_chat, thread, "run-1");
        _chat.SeedRun("run-1", "running", finished: false);
        _chat.LiveQuestions.Latest["run-1"] = new DialogQuestion(
            questionId, QuestionType.Approval, "Ship?", null, null, null, TimeSpan.FromHours(1));
        await _chat.Get<ChatRunFollower>().FollowOnceAsync(default);
    }

    private SlackInteractionHandler SlackHandler() => new(
        _chat.Get<ChatRunAnswerRouter>(), null!, null!,
        new SlackAdapter(
            new SlackApiClient(new HttpClient(_slackApi), new SlackAdapterOptions { BotToken = "t" },
                NullLogger<SlackApiClient>.Instance),
            new SlackTypedQuestionBlockBuilder(), new SlackMessageBlockBuilder(),
            NullLogger<SlackAdapter>.Instance),
        NullLogger<SlackInteractionHandler>.Instance);

    private static TeamsAdapter TeamsAdapter() => new(
        null!, new TeamsTypedQuestionTracker(), null!, NullLogger<TeamsAdapter>.Instance);
}
