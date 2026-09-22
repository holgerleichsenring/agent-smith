using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Adapters;
using FluentAssertions;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// Tests Block Kit JSON generation for typed questions
/// and timeout handling for AskTypedQuestionAsync.
/// </summary>
public sealed class SlackTypedQuestionTests
{
    private static DialogQuestion CreateQuestion(
        QuestionType type,
        string questionId = "q-1",
        string text = "Do you approve?",
        string? context = null,
        IReadOnlyList<string>? choices = null,
        string? defaultAnswer = null,
        TimeSpan? timeout = null)
    {
        var rich = choices?.Select(c => new DialogChoice(c)).ToList();
        return new DialogQuestion(
            questionId,
            type,
            text,
            context,
            rich,
            defaultAnswer,
            timeout ?? TimeSpan.FromSeconds(30));
    }

    // --- Block Kit generation tests ---

    [Fact]
    public void BuildTypedQuestionBlocks_Confirmation_ContainsYesAndNoButtons()
    {
        var question = CreateQuestion(QuestionType.Confirmation);

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("q-1:yes");
        json.Should().Contain("q-1:no");
        json.Should().Contain("primary");
        json.Should().Contain("danger");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Confirmation_ContainsQuestionText()
    {
        var question = CreateQuestion(QuestionType.Confirmation, text: "Continue with merge?");

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Continue with merge?");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Confirmation_WithContext_IncludesContextBlock()
    {
        var question = CreateQuestion(QuestionType.Confirmation, context: "Branch has 3 conflicts");

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Branch has 3 conflicts");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Choice_CreatesButtonPerChoice()
    {
        var choices = new[] { "Option A", "Option B", "Option C" };
        var question = CreateQuestion(QuestionType.Choice, choices: choices);

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Option A");
        json.Should().Contain("Option B");
        json.Should().Contain("Option C");
        json.Should().Contain("q-1:0");
        json.Should().Contain("q-1:1");
        json.Should().Contain("q-1:2");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Approval_ContainsApproveAndRejectButtons()
    {
        var question = CreateQuestion(QuestionType.Approval);

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("q-1:approve");
        json.Should().Contain("q-1:reject");
        json.Should().Contain("Approve");
        json.Should().Contain("Reject");
        json.Should().Contain("optional comment");
    }

    /// <summary>
    /// 2026-09-22-355b: this surface never sends a button's VALUE — it splits the action id at
    /// its LAST colon — so the label has to travel in the action id, and it has to come back
    /// out of the extractor whole. A colon in a label would split it in the wrong place, the
    /// question id would stop matching, and the click would vanish in silence.
    /// </summary>
    [Fact]
    public void SlackApproval_AShapeButton_CarriesItsLabelBackAsTheAnswer()
    {
        const string label = "Cut into several phases";
        var question = CreateQuestion(
            QuestionType.Approval, questionId: "0123456789abcdef0123456789abcdef",
            choices: [label]);

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var actions = JsonNode.Parse(JsonSerializer.Serialize(blocks))!
            .AsArray().Single(block => block!["type"]!.GetValue<string>() == "actions")!;
        var elements = actions["elements"]!.AsArray();

        // The shapes ride BESIDE the approve/reject pair, in one action block.
        elements.Select(e => e!["action_id"]!.GetValue<string>()).Should().Equal(
            "0123456789abcdef0123456789abcdef:approve",
            "0123456789abcdef0123456789abcdef:reject",
            $"0123456789abcdef0123456789abcdef:{label}");
        var clicked = JsonNode.Parse(
            $$"""
            {"channel":{"id":"C1"},"actions":[{"action_id":"{{elements[2]!["action_id"]!.GetValue<string>()}}"}]}
            """)!;

        var (_, questionId, answer) = SlackPayloadExtractor.ExtractInteractionFields(clicked);

        questionId.Should().Be("0123456789abcdef0123456789abcdef");
        answer.Should().Be(label, "the picked shape is the edit note the master re-proposes with");
    }

    [Fact]
    public void SlackApproval_WithShapes_SaysAPickBuysATurn()
    {
        var json = JsonSerializer.Serialize(new SlackTypedQuestionBlockBuilder()
            .Build(CreateQuestion(QuestionType.Approval, choices: ["Cut into several phases"])));

        json.Should().Contain("starts a new turn and files nothing");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_FreeText_ContainsPromptToTypeAnswer()
    {
        var question = CreateQuestion(QuestionType.FreeText, text: "What is the target branch?");

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("What is the target branch?");
        json.Should().Contain("type your answer");
        // FreeText should NOT contain action buttons
        json.Should().NotContain("\"type\":\"button\"");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_FreeText_WithContext_IncludesContextBlock()
    {
        var question = CreateQuestion(QuestionType.FreeText, context: "Current branch: feature/xyz");

        var blocks = new SlackTypedQuestionBlockBuilder().Build(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Current branch: feature/xyz");
    }

    // --- TryCompleteTypedQuestion / HasPendingTypedQuestion ---

    [Fact]
    public void TryCompleteTypedQuestion_WithoutPending_ReturnsFalse()
    {
        var adapter = CreateAdapter();
        var answer = new DialogAnswer("q-1", "yes", null, DateTimeOffset.UtcNow, "U123");

        adapter.TryCompleteTypedQuestion("q-1", answer).Should().BeFalse();
    }

    [Fact]
    public void HasPendingTypedQuestion_WithoutPending_ReturnsFalse()
    {
        var adapter = CreateAdapter();

        adapter.HasPendingTypedQuestion("q-1").Should().BeFalse();
    }

    // --- Timeout handling ---

    [Fact]
    public async Task AskTypedQuestionAsync_InfoType_ReturnsNullImmediately()
    {
        var handler = new MockHttpMessageHandler(SlackOkResponse());
        var adapter = CreateAdapter(handler);
        var question = CreateQuestion(QuestionType.Info, text: "Build started");

        var result = await adapter.AskTypedQuestionAsync("C123", question, threadId: null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AskTypedQuestionAsync_Timeout_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(SlackOkResponse());
        var adapter = CreateAdapter(handler);
        var question = CreateQuestion(
            QuestionType.Confirmation,
            timeout: TimeSpan.FromMilliseconds(100));

        var result = await adapter.AskTypedQuestionAsync("C123", question, threadId: null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AskTypedQuestionAsync_AnswerBeforeTimeout_ReturnsAnswer()
    {
        var handler = new MockHttpMessageHandler(SlackOkResponse());
        var adapter = CreateAdapter(handler);
        // The question's OWN timeout is not what this test is about, so it is set past the
        // point where waiting means a hang — five seconds made "an answer arrives first" a
        // claim about how quickly the host could get round to delivering it.
        var question = CreateQuestion(
            QuestionType.Confirmation,
            questionId: "q-answer-test",
            timeout: TestWaits.Hang);

        // Start the question in background
        var questionTask = adapter.AskTypedQuestionAsync("C123", question, threadId: null, CancellationToken.None);

        // The question registers its TCS only AFTER the async chat.postMessage completes, so
        // a fixed delay races on loaded runners. Poll until the completion succeeds instead —
        // TryComplete returns false until the question is registered, then sets the answer.
        // 2026-09-22-3f7c: the poll used to give up after a thousand milliseconds of its own,
        // which is the same fixed delay wearing a loop.
        var answer = new DialogAnswer("q-answer-test", "yes", null, DateTimeOffset.UtcNow, "U456");
        var completed = await TestWaits.ReachedAsync(
            () => adapter.TryCompleteTypedQuestion("q-answer-test", answer));

        completed.Should().BeTrue("the question registers itself and then takes its answer");

        var result = await questionTask;
        result.Should().NotBeNull();
        result!.Answer.Should().Be("yes");
        result.AnsweredBy.Should().Be("U456");
    }

    [Fact]
    public async Task AskTypedQuestionAsync_CleansUpPendingOnTimeout()
    {
        var handler = new MockHttpMessageHandler(SlackOkResponse());
        var adapter = CreateAdapter(handler);
        var question = CreateQuestion(
            QuestionType.Confirmation,
            questionId: "q-cleanup",
            timeout: TimeSpan.FromMilliseconds(100));

        await adapter.AskTypedQuestionAsync("C123", question, threadId: null, CancellationToken.None);

        // After timeout, the pending question should be cleaned up
        adapter.HasPendingTypedQuestion("q-cleanup").Should().BeFalse();
    }

    // --- Helpers ---

    private static SlackAdapter CreateAdapter(MockHttpMessageHandler? handler = null)
    {
        handler ??= new MockHttpMessageHandler(SlackOkResponse());
        var httpClient = new HttpClient(handler);
        var options = new AgentSmith.Server.Models.SlackAdapterOptions
        {
            BotToken = "xoxb-test-token",
            SigningSecret = "test-secret"
        };
        var apiClient = new SlackApiClient(
            httpClient,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SlackApiClient>.Instance);
        return new SlackAdapter(
            apiClient,
            new SlackTypedQuestionBlockBuilder(),
            new SlackMessageBlockBuilder(),
            new SlackProgressFormatter(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SlackAdapter>.Instance);
    }

    private static string SlackOkResponse() =>
        """{"ok":true,"ts":"1234567890.123456"}""";

    /// <summary>
    /// Simple HTTP message handler that returns a fixed response.
    /// </summary>
    private sealed class MockHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
