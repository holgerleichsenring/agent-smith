using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0388a: the producer stamps step membership. PipelineStepRunner opens the
/// ambient step scope around the whole step, and the publish-path decorator
/// reads it onto every event — including events raised on child async flows
/// (sub-agent work), which inherit the spawning step through the AsyncLocal.
/// Outside any step the stamp stays null; nothing infers a step.
/// </summary>
public sealed class StepAttributionTests
{
    private readonly Mock<ICommandExecutor> _executor = new();
    private readonly Mock<ICommandContextFactory> _factory = new();
    private readonly RecordingEventPublisher _recorder = new();
    private readonly IRunContextAccessor _runContext = new AsyncLocalRunContextAccessor();

    private IEventPublisher Publisher => new StepAttributingEventPublisher(_recorder, _runContext);

    [Fact]
    public async Task StepScope_EventPublishedInsideStep_CarriesThatStepIndex()
    {
        await RunStepAsync(stepIndex: 3, insideStep: () => PublishDecisionAsync());

        Recorded<DecisionLoggedEvent>().OriginStepIndex.Should().Be(3);
    }

    [Fact]
    public async Task StepScope_SubAgentEventFromChildTask_InheritsTheSpawningStepIndex()
    {
        // The sub-agent's work runs on a child task started inside the step —
        // the AsyncLocal frame flows there, so no per-emit plumbing is needed.
        await RunStepAsync(stepIndex: 7, insideStep: () => Task.Run(PublishSubAgentSpawnedAsync));

        Recorded<SubAgentSpawnedEvent>().OriginStepIndex.Should().Be(7);
    }

    [Fact]
    public async Task StepScope_EventOutsideAnyStep_HasNullStepIndex()
    {
        await PublishDecisionAsync();

        Recorded<DecisionLoggedEvent>().OriginStepIndex.Should().BeNull();
    }

    private Task PublishDecisionAsync() =>
        Publisher.PublishAsync(new DecisionLoggedEvent(
            "run-1", "tooling", "sqlite", "postgres", "smallest footprint", DateTimeOffset.UtcNow));

    private Task PublishSubAgentSpawnedAsync() =>
        Publisher.PublishAsync(new SubAgentSpawnedEvent(
            "run-1", "sub-1", "analyzer", "scanning", null, "hash", DateTimeOffset.UtcNow));

    private TEvent Recorded<TEvent>() where TEvent : RunEvent =>
        _recorder.Events.OfType<TEvent>().Should().ContainSingle().Subject;

    // Drives the REAL step seam: PipelineStepRunner opens the scope, the command
    // executor stands in for the step's handler and publishes from inside it.
    private async Task RunStepAsync(int stepIndex, Func<Task> insideStep)
    {
        var commands = new LinkedList<PipelineCommand>(new[] { PipelineCommand.Simple("Analyze") });
        var project = new ResolvedProject();
        var context = new PipelineContext();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<ICommandContext>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await insideStep(); return CommandResult.Ok("done"); });

        await NewRunner().RunSingleAsync(
            commands.First!, commands, project, context, stepIndex, CancellationToken.None);
    }

    private PipelineStepRunner NewRunner() =>
        new(_executor.Object, _factory.Object, Mock.Of<IProgressReporter>(),
            new DataFlowReadGate(
                new PhaseDataFlowResolver(Array.Empty<IPhaseDataFlow>()),
                Microsoft.Extensions.Options.Options.Create(new PipelineDataFlowConfig()),
                NullLogger<DataFlowReadGate>.Instance),
            Publisher, _runContext, NullLogger<PipelineStepRunner>.Instance);
}
