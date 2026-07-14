using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Resume;

/// <summary>
/// p0327: the hybrid wait. Below the hot threshold the ask stays in-memory;
/// past it an eligible ticket run checkpoints and parks; a resume-delivered
/// answer is consumed exactly once instead of re-asking.
/// </summary>
public sealed class DialogueAskGateTests
{
    private readonly Mock<IDialogueTransport> _transport = new();
    private readonly Mock<IDialogueCheckpointWriter> _checkpointWriter = new();
    private readonly Mock<IProgressReporter> _reporter = new();
    private readonly InMemoryDialogueTrail _trail = new();
    private readonly DialogueAskGate _sut;

    public DialogueAskGateTests()
    {
        _reporter.Setup(r => r.JobId).Returns("job-1");
        _sut = new DialogueAskGate(
            _transport.Object, _trail, _checkpointWriter.Object, _reporter.Object,
            NullLogger<DialogueAskGate>.Instance);
    }

    [Fact]
    public async Task Ask_PastHotThreshold_CheckpointsAndEndsWorker()
    {
        var pipeline = TicketPipeline(hotWaitSeconds: 0);
        var question = Question(TimeSpan.FromDays(3));
        _transport.Setup(t => t.WaitForAnswerAsync(
                "job-1", question.QuestionId, TimeSpan.Zero, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);
        _checkpointWriter.Setup(w => w.TryCheckpointAsync(
                pipeline, question, "job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var outcome = await _sut.AskAsync(pipeline, question, CancellationToken.None);

        outcome.Checkpointed.Should().BeTrue();
        outcome.Answer.Should().BeNull();
        pipeline.Get<bool>(ContextKeys.WaitingForInput).Should().BeTrue(
            "the executor's parked-reason check ends the worker cleanly");
        // The hot wait used the THRESHOLD, not the days-scale question timeout.
        _transport.Verify(t => t.WaitForAnswerAsync(
            "job-1", question.QuestionId, TimeSpan.Zero, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ask_AnsweredWithinHotWindow_NoCheckpoint()
    {
        var pipeline = TicketPipeline(hotWaitSeconds: 600);
        var question = Question(TimeSpan.FromDays(3));
        var answer = new DialogAnswer(question.QuestionId, "yes", null, DateTimeOffset.UtcNow, "@op");
        _transport.Setup(t => t.WaitForAnswerAsync(
                "job-1", question.QuestionId, TimeSpan.FromSeconds(600), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var outcome = await _sut.AskAsync(pipeline, question, CancellationToken.None);

        outcome.Checkpointed.Should().BeFalse();
        outcome.Answer!.Answer.Should().Be("yes");
        pipeline.Has(ContextKeys.WaitingForInput).Should().BeFalse();
        _checkpointWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Ask_NonTicketRun_NeverCheckpoints_WaitsFullTimeout()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.DialogueHotWaitSeconds, 0);
        var question = Question(TimeSpan.FromMinutes(5));
        _transport.Setup(t => t.WaitForAnswerAsync(
                "job-1", question.QuestionId, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialogAnswer?)null);

        var outcome = await _sut.AskAsync(pipeline, question, CancellationToken.None);

        outcome.Checkpointed.Should().BeFalse();
        outcome.Answer!.AnsweredBy.Should().Be("system");
        outcome.Answer.Comment.Should().Be("timeout");
        _checkpointWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Ask_ResumedAnswer_ConsumedOnceWithoutRepublishing()
    {
        var pipeline = TicketPipeline(hotWaitSeconds: 0);
        var delivered = new DialogAnswer("original-q", "approve", null, DateTimeOffset.UtcNow, "@op");
        pipeline.Set(ContextKeys.ResumedDialogueAnswer, delivered);
        var question = Question(TimeSpan.FromDays(3)); // re-minted id on re-entry

        var outcome = await _sut.AskAsync(pipeline, question, CancellationToken.None);

        outcome.Checkpointed.Should().BeFalse();
        outcome.Answer!.Answer.Should().Be("approve");
        outcome.Answer.QuestionId.Should().Be(question.QuestionId,
            "the delivered answer is re-keyed to the current ask");
        pipeline.Has(ContextKeys.ResumedDialogueAnswer).Should().BeFalse("consumed exactly once");
        _transport.Verify(t => t.PublishQuestionAsync(
            It.IsAny<string>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ask_NoJobId_FallsBackToRunIdIdentity()
    {
        _reporter.Setup(r => r.JobId).Returns((string?)null);
        var pipeline = TicketPipeline(hotWaitSeconds: 600);
        var question = Question(TimeSpan.FromMinutes(1));
        var answer = new DialogAnswer(question.QuestionId, "yes", null, DateTimeOffset.UtcNow, "@op");
        _transport.Setup(t => t.WaitForAnswerAsync(
                "run-1", question.QuestionId, TimeSpan.FromMinutes(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var outcome = await _sut.AskAsync(pipeline, question, CancellationToken.None);

        outcome.Answer!.Answer.Should().Be("yes");
        _transport.Verify(t => t.PublishQuestionAsync(
            "run-1", question, It.IsAny<CancellationToken>()), Times.Once,
            "in-process server runs have no --job-id; the run id is the dialogue identity");
    }

    private static PipelineContext TicketPipeline(int hotWaitSeconds)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        pipeline.Set(ContextKeys.DialogueHotWaitSeconds, hotWaitSeconds);
        return pipeline;
    }

    private static DialogQuestion Question(TimeSpan timeout) => new(
        Guid.NewGuid().ToString("N"), QuestionType.Approval, "Approve?", null, null, "reject", timeout);
}
