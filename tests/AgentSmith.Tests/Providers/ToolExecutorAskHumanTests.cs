using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers;

public sealed class ToolExecutorAskHumanTests : IDisposable
{
    private readonly string _repoPath;
    private readonly Mock<IDialogueTransport> _transport = new();
    private readonly InMemoryDialogueTrail _trail = new();
    private const string JobId = "test-job-42";

    public ToolExecutorAskHumanTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "agentsmith-ask-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
            Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public async Task AskHuman_WithAnswer_ReturnsAnswerText()
    {
        var answer = new DialogAnswer("ignored", "option-b", null, DateTimeOffset.UtcNow, "@holger");
        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var sut = CreateExecutor();
        var input = BuildInput("choice", "Which option?", "Need domain knowledge", "option-a",
            new JsonArray("option-a", "option-b"));

        var result = await sut.ExecuteAsync("ask_human", input);

        result.Should().Contain("Answer: option-b");
        result.Should().NotContain("timeout");

        _transport.Verify(t => t.PublishQuestionAsync(
            JobId, It.Is<DialogQuestion>(q =>
                q.Type == QuestionType.Choice &&
                q.Text == "Which option?" &&
                q.Choices!.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AskHuman_Timeout_ReturnsDefaultAnswer()
    {
        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);

        var sut = CreateExecutor();
        var input = BuildInput("confirmation", "Continue?", "Ambiguous spec", "yes");

        var result = await sut.ExecuteAsync("ask_human", input);

        result.Should().Contain("timeout");
        result.Should().Contain("yes");
    }

    [Fact]
    public async Task AskHuman_RecordsInTrail()
    {
        var answer = new DialogAnswer("ignored", "proceed", null, DateTimeOffset.UtcNow, "@user");
        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var sut = CreateExecutor();
        var input = BuildInput("approval", "Approve deploy?", "Breaking change", "no");

        await sut.ExecuteAsync("ask_human", input);

        var entries = _trail.GetAll();
        entries.Should().HaveCount(1);
        entries[0].Question.Text.Should().Be("Approve deploy?");
        entries[0].Question.Type.Should().Be(QuestionType.Approval);
        entries[0].Answer.Answer.Should().Be("proceed");
    }

    [Fact]
    public async Task AskHuman_TimeoutRecordsInTrail()
    {
        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);

        var sut = CreateExecutor();
        var input = BuildInput("confirmation", "Continue?", "Test", "yes");

        await sut.ExecuteAsync("ask_human", input);

        var entries = _trail.GetAll();
        entries.Should().HaveCount(1);
        entries[0].Answer.Comment.Should().Be("timeout");
        entries[0].Answer.Answer.Should().Be("yes");
    }

    [Fact]
    public async Task AskHuman_NoTransport_ReturnsError()
    {
        var sut = new ToolExecutor(_repoPath, NullLogger.Instance);
        var input = BuildInput("confirmation", "Continue?", "Test", "yes");

        var result = await sut.ExecuteAsync("ask_human", input);

        result.Should().StartWith("Error:");
        result.Should().Contain("not configured");
    }

    [Fact]
    public async Task AskHuman_InvalidQuestionType_ReturnsError()
    {
        var sut = CreateExecutor();
        var input = new JsonObject
        {
            ["question_type"] = "invalid_type",
            ["text"] = "Test?",
            ["context"] = "Test",
            ["default_answer"] = "yes"
        };

        var result = await sut.ExecuteAsync("ask_human", input);

        result.Should().StartWith("Error:");
        result.Should().Contain("invalid_type");
    }

    [Fact]
    public async Task AskHuman_FreeText_ParsesCorrectly()
    {
        var answer = new DialogAnswer("ignored", "MyClassName", null, DateTimeOffset.UtcNow, "@dev");
        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var sut = CreateExecutor();
        var input = BuildInput("free_text", "Class name?", "Domain naming", "DefaultName");

        var result = await sut.ExecuteAsync("ask_human", input);

        result.Should().Be("Answer: MyClassName");

        _transport.Verify(t => t.PublishQuestionAsync(
            JobId, It.Is<DialogQuestion>(q => q.Type == QuestionType.FreeText),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private ToolExecutor CreateExecutor() =>
        new(_repoPath, NullLogger.Instance, dialogueTransport: _transport.Object,
            dialogueTrail: _trail, jobId: JobId);

    private static JsonObject BuildInput(
        string questionType, string text, string context, string defaultAnswer,
        JsonArray? choices = null)
    {
        var obj = new JsonObject
        {
            ["question_type"] = questionType,
            ["text"] = text,
            ["context"] = context,
            ["default_answer"] = defaultAnswer
        };
        if (choices is not null)
            obj["choices"] = choices;
        return obj;
    }
}
