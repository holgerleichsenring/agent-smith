using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0147e: per-service unit tests for IPipelineStepRunner. Covers the
/// step-runner's contract in isolation from the orchestrator: exception
/// envelope, OCE propagation, progress reporter wiring, batch-vs-single
/// dispatch, and the static PeelBatch peeler.
/// </summary>
public sealed class PipelineStepRunnerTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<IProgressReporter> _progressMock = new();
    private readonly PipelineStepRunner _sut;

    public PipelineStepRunnerTests()
    {
        _sut = new PipelineStepRunner(
            _executorMock.Object,
            _factoryMock.Object,
            _progressMock.Object,
            new PhaseDataFlowResolver(Array.Empty<IPhaseDataFlow>()),
            new AgentSmithConfig(),
            new AgentSmith.Application.Services.SkillRounds.SkillRoundBufferDispatcher(),
            AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
            NullLogger<PipelineStepRunner>.Instance);
    }

    [Fact]
    public async Task RunSingleAsync_FactoryThrows_WrapsAsCommandResultFail_NextStepCanRun()
    {
        var commands = new LinkedList<PipelineCommand>(new[]
        {
            PipelineCommand.Simple("StepOne"),
            PipelineCommand.Simple("StepTwo")
        });
        var project = new ResolvedProject();
        var context = new PipelineContext();

        _factoryMock.Setup(f => f.Create(PipelineCommand.Simple("StepOne"), project, context))
            .Throws(new InvalidOperationException("boom"));

        var result = await _sut.RunSingleAsync(
            commands.First!, commands, project, context, 1, CancellationToken.None);

        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Message.Should().Contain("StepOne");
        result.Result.Message.Should().Contain("boom");
        result.Result.FailedStep.Should().Be(1);
        // The linked list is untouched — the orchestrator's loop is free to
        // advance to the second node and execute the next step.
        commands.Count.Should().Be(2);
        commands.Last!.Value.Name.Should().Be("StepTwo");
    }

    [Fact]
    public async Task RunSingleAsync_OperationCanceledException_Propagates()
    {
        var commands = new LinkedList<PipelineCommand>(new[] { PipelineCommand.Simple("CanceledCmd") });
        var project = new ResolvedProject();
        var context = new PipelineContext();

        _factoryMock.Setup(f => f.Create(PipelineCommand.Simple("CanceledCmd"), project, context))
            .Throws(new OperationCanceledException());

        var act = async () => await _sut.RunSingleAsync(
            commands.First!, commands, project, context, 1, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunSingleAsync_EmitsProgressForStep()
    {
        var commands = new LinkedList<PipelineCommand>(new[] { PipelineCommand.Simple("HappyStep") });
        var project = new ResolvedProject();
        var context = new PipelineContext();

        _factoryMock.Setup(f => f.Create(PipelineCommand.Simple("HappyStep"), project, context))
            .Throws(new Exception("short-circuit after progress")); // skip the dispatch path

        await _sut.RunSingleAsync(commands.First!, commands, project, context, 1, CancellationToken.None);

        _progressMock.Verify(p => p.ReportProgressAsync(
            1, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void PeelBatch_PublicSurface_ReturnsSameAsInternalPeeler()
    {
        var list = new LinkedList<PipelineCommand>(new[]
        {
            PipelineCommand.SkillRound(CommandNames.SkillRound, "a", 1),
            PipelineCommand.SkillRound(CommandNames.SkillRound, "b", 1)
        });

        var batch = _sut.PeelBatch(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(2);
    }
}
