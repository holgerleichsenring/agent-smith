using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-12-3a36c: the finalizer tail's CommitAndPR commits a failed run's work under the WIP
/// message, whose Failed-Step trailer must name the step — so the step is known BEFORE the
/// tail runs, on a failing step and on a safety cancel alike, not only once the error handler
/// set it afterwards.
/// </summary>
public sealed class PipelineExecutorFailedStepTests
{
    [Fact]
    public async Task CommitAndPR_FailedStep_WipMessageNamesStep()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple("BadCommand"), project, pipeline))
            .Throws(new Exception("per-pipeline cost budget exhausted"));
        var seen = CaptureWipMessageAtTheTail(h, project, pipeline);

        await h.Sut.ExecuteAsync(["BadCommand", CommandNames.CommitAndPR], project, pipeline, CancellationToken.None);

        seen().Should().Contain(
            $"Failed-Step: {StepLabelComposer.Label(PipelineCommand.Simple("BadCommand"))}");
    }

    [Fact]
    public async Task CommitAndPR_SafetyCancelWithCode_WipMessageNamesStep()
    {
        var h = new PipelineExecutorTestBuilder();
        var project = new ResolvedProject();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        var reason = "watchdog-wall-time";
        h.CancellationRegistryMock.Setup(r => r.TryGetReason("run-1", out reason)).Returns(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple("Work"), project, pipeline))
            .Throws(new OperationCanceledException());
        var seen = CaptureWipMessageAtTheTail(h, project, pipeline);

        var act = async () => await h.Sut.ExecuteAsync(
            ["Work", CommandNames.CommitAndPR], project, pipeline, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        seen().Should().Contain($"Failed-Step: {StepLabelComposer.Label(PipelineCommand.Simple("Work"))}");
    }

    private static Func<string?> CaptureWipMessageAtTheTail(
        PipelineExecutorTestBuilder h, ResolvedProject project, PipelineContext pipeline)
    {
        string? message = null;
        var tail = new Mock<ICommandContext>();
        h.FactoryMock.Setup(f => f.Create(PipelineCommand.Simple(CommandNames.CommitAndPR), project, pipeline))
            .Returns(tail.Object);
        h.ExecutorMock.Setup(e => e.ExecuteAsync(tail.Object, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                message = new WipCommitMessage().Build(pipeline);
                return Task.FromResult(CommandResult.Ok("persisted"));
            });
        return () => message;
    }
}
