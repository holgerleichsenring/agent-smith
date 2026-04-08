using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Dispatcher.Services.Adapters;
using FluentAssertions;

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
        return new DialogQuestion(
            questionId,
            type,
            text,
            context,
            choices,
            defaultAnswer,
            timeout ?? TimeSpan.FromSeconds(30));
    }

    // --- Block Kit generation tests ---

    [Fact]
    public void BuildTypedQuestionBlocks_Confirmation_ContainsYesAndNoButtons()
    {
        var question = CreateQuestion(QuestionType.Confirmation);

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
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

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Continue with merge?");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Confirmation_WithContext_IncludesContextBlock()
    {
        var question = CreateQuestion(QuestionType.Confirmation, context: "Branch has 3 conflicts");

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("Branch has 3 conflicts");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_Choice_CreatesButtonPerChoice()
    {
        var choices = new[] { "Option A", "Option B", "Option C" };
        var question = CreateQuestion(QuestionType.Choice, choices: choices);

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
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

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
        var json = JsonSerializer.Serialize(blocks);

        json.Should().Contain("q-1:approve");
        json.Should().Contain("q-1:reject");
        json.Should().Contain("Approve");
        json.Should().Contain("Reject");
        json.Should().Contain("optional comment");
    }

    [Fact]
    public void BuildTypedQuestionBlocks_FreeText_ContainsPromptToTypeAnswer()
    {
        var question = CreateQuestion(QuestionType.FreeText, text: "What is the target branch?");

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
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

        var blocks = SlackAdapter.BuildTypedQuestionBlocks(question);
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

        var result = await adapter.AskTypedQuestionAsync("C123", question, CancellationToken.None);

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

        var result = await adapter.AskTypedQuestionAsync("C123", question, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AskTypedQuestionAsync_AnswerBeforeTimeout_ReturnsAnswer()
    {
        var handler = new MockHttpMessageHandler(SlackOkResponse());
        var adapter = CreateAdapter(handler);
        var question = CreateQuestion(
            QuestionType.Confirmation,
            questionId: "q-answer-test",
            timeout: TimeSpan.FromSeconds(5));

        // Start the question in background
        var questionTask = adapter.AskTypedQuestionAsync("C123", question, CancellationToken.None);

        // Give it a moment to register the TCS
        await Task.Delay(50);

        // Complete the question
        var answer = new DialogAnswer("q-answer-test", "yes", null, DateTimeOffset.UtcNow, "U456");
        adapter.TryCompleteTypedQuestion("q-answer-test", answer).Should().BeTrue();

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

        await adapter.AskTypedQuestionAsync("C123", question, CancellationToken.None);

        // After timeout, the pending question should be cleaned up
        adapter.HasPendingTypedQuestion("q-cleanup").Should().BeFalse();
    }

    // --- Helpers ---

    private static SlackAdapter CreateAdapter(MockHttpMessageHandler? handler = null)
    {
        handler ??= new MockHttpMessageHandler(SlackOkResponse());
        var httpClient = new HttpClient(handler);
        var options = new AgentSmith.Dispatcher.Models.SlackAdapterOptions
        {
            BotToken = "xoxb-test-token",
            SigningSecret = "test-secret"
        };
        return new SlackAdapter(
            httpClient,
            options,
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
