using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class AskCommandHandlerTests
{
    private readonly Mock<IDialogueTransport> _transport = new();
    private readonly InMemoryDialogueTrail _trail = new();
    private readonly Mock<IProgressReporter> _reporter = new();
    private readonly AskCommandHandler _sut;
    private const string JobId = "job-123";

    public AskCommandHandlerTests()
    {
        _reporter.Setup(r => r.JobId).Returns(JobId);
        _sut = new AskCommandHandler(
            _transport.Object, _trail, _reporter.Object,
            NullLogger<AskCommandHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsOkAndStoresAnswer()
    {
        var question = CreateQuestion(QuestionType.Confirmation, "Proceed?");
        var answer = new DialogAnswer(question.QuestionId, "yes", null, DateTimeOffset.UtcNow, "@holger");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var pipeline = new PipelineContext();
        var context = new AskContext(question, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("yes");
        pipeline.Get<string>(ContextKeys.DialogueAnswer).Should().Be("yes");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_UsesDefaultAndReturnsOk()
    {
        var question = CreateQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "yes");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);

        var pipeline = new PipelineContext();
        var context = new AskContext(question, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DialogueAnswer).Should().Be("yes");
    }

    [Fact]
    public async Task ExecuteAsync_ApprovalRejected_ReturnsFail()
    {
        var question = CreateQuestion(QuestionType.Approval, "Approve breaking change?");
        var answer = new DialogAnswer(question.QuestionId, "no", null, DateTimeOffset.UtcNow, "@reviewer");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var pipeline = new PipelineContext();
        var context = new AskContext(question, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Rejected");
        result.Message.Should().Contain("@reviewer");
    }

    [Fact]
    public async Task ExecuteAsync_ApprovalApproved_ReturnsOk()
    {
        var question = CreateQuestion(QuestionType.Approval, "Approve deploy?");
        var answer = new DialogAnswer(question.QuestionId, "yes", null, DateTimeOffset.UtcNow, "@reviewer");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var pipeline = new PipelineContext();
        var context = new AskContext(question, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RecordsInDialogueTrail()
    {
        var question = CreateQuestion(QuestionType.FreeText, "Branch name?");
        var answer = new DialogAnswer(question.QuestionId, "feature/login", null, DateTimeOffset.UtcNow, "@dev");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var context = new AskContext(question, new PipelineContext());

        await _sut.ExecuteAsync(context, CancellationToken.None);

        var entries = _trail.GetAll();
        entries.Should().HaveCount(1);
        entries[0].Question.Text.Should().Be("Branch name?");
        entries[0].Answer.Answer.Should().Be("feature/login");
    }

    [Fact]
    public async Task ExecuteAsync_NoJobId_UsesDefaultAnswer()
    {
        var noJobReporter = new Mock<IProgressReporter>();
        noJobReporter.Setup(r => r.JobId).Returns((string?)null);

        var sut = new AskCommandHandler(
            _transport.Object, _trail, noJobReporter.Object,
            NullLogger<AskCommandHandler>.Instance);

        var question = CreateQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "yes");
        var context = new AskContext(question, new PipelineContext());

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("default");

        _transport.Verify(t => t.PublishQuestionAsync(
            It.IsAny<string>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("reject")]
    [InlineData("denied")]
    [InlineData("n")]
    public async Task ExecuteAsync_ApprovalVariousRejectionWords_ReturnsFail(string rejectionWord)
    {
        var question = CreateQuestion(QuestionType.Approval, "Approve?");
        var answer = new DialogAnswer(question.QuestionId, rejectionWord, null, DateTimeOffset.UtcNow, "@user");

        _transport.Setup(t => t.WaitForAnswerAsync(
                JobId, question.QuestionId, question.Timeout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var context = new AskContext(question, new PipelineContext());

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static DialogQuestion CreateQuestion(
        QuestionType type, string text, string defaultAnswer = "yes") =>
        new(Guid.NewGuid().ToString("N"), type, text, "Test context",
            null, defaultAnswer, TimeSpan.FromMinutes(5));
}
