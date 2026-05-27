using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0169e sequence-completeness gate. Each producer category gets a row
/// in <see cref="DropOneProducer_RemainingTypesPresent"/>: when a producer
/// is given a no-op publisher (it "goes silent"), the event types that
/// producer is solely responsible for must drop out of the recorded set.
/// A future regression where a producer stops emitting fails at exactly
/// one row instead of slipping through because the other producers still
/// emit.
/// </summary>
public sealed class EventSequenceCompletenessTests
{
    private const string RunId = "2026-05-27T10-00-00-aaaa";

    [Theory]
    [InlineData(ProducerId.PipelineExecutorRun, EventType.RunStarted, EventType.RunFinished)]
    [InlineData(ProducerId.PipelineStepRunner, EventType.StepStarted, EventType.StepFinished)]
    [InlineData(ProducerId.SandboxCoordinator, EventType.SandboxCreated)]
    [InlineData(ProducerId.SandboxEventProjector, EventType.SandboxCommand, EventType.SandboxResult)]
    [InlineData(ProducerId.LlmDecorator, EventType.LlmCallStarted, EventType.LlmCallFinished)]
    [InlineData(ProducerId.ToolDecorator, EventType.ToolCall, EventType.ToolResult)]
    [InlineData(ProducerId.DecisionLogger, EventType.DecisionLogged)]
    [InlineData(ProducerId.GateHandlers, EventType.GateChecked)]
    [InlineData(ProducerId.TriageOutputProducer, EventType.TriageRoute)]
    public async Task DropOneProducer_RemainingTypesPresent(ProducerId dropped, params EventType[] missingTypes)
    {
        var recorder = new RecordingEventPublisher();
        var droppingPublisher = new NoOpEventPublisher();

        foreach (ProducerId producer in Enum.GetValues<ProducerId>())
        {
            var publisher = producer == dropped ? (IEventPublisher)droppingPublisher : recorder;
            await ExerciseProducerAsync(producer, publisher);
        }

        foreach (var missing in missingTypes)
            recorder.Types.Should().NotContain(missing,
                $"because producer {dropped} was the only one emitting {missing} — its drop should surface here");

        // Sanity: every OTHER expected type must be present.
        var allExpected = AllEmittedTypes().Except(missingTypes).ToArray();
        recorder.Types.Should().Contain(allExpected,
            $"because all producers other than {dropped} were live");
    }

    private static IReadOnlyList<EventType> AllEmittedTypes() => new[]
    {
        EventType.RunStarted, EventType.RunFinished,
        EventType.StepStarted, EventType.StepFinished,
        EventType.SandboxCreated,
        EventType.SandboxCommand, EventType.SandboxResult,
        EventType.LlmCallStarted, EventType.LlmCallFinished,
        EventType.ToolCall, EventType.ToolResult,
        EventType.DecisionLogged, EventType.GateChecked,
        EventType.TriageRoute
    };

    private static Task ExerciseProducerAsync(ProducerId producer, IEventPublisher publisher) =>
        producer switch
        {
            ProducerId.PipelineExecutorRun => ExerciseRunLifecycle(publisher),
            ProducerId.PipelineStepRunner => ExerciseStepRunner(publisher),
            ProducerId.SandboxCoordinator => ExerciseSandboxCoordinator(publisher),
            ProducerId.SandboxEventProjector => ExerciseSandboxProjector(publisher),
            ProducerId.LlmDecorator => ExerciseLlmDecorator(publisher),
            ProducerId.ToolDecorator => ExerciseToolDecorator(publisher),
            ProducerId.DecisionLogger => ExerciseDecisionLogger(publisher),
            ProducerId.GateHandlers => ExerciseGateHandlers(publisher),
            ProducerId.TriageOutputProducer => ExerciseTriageProducer(publisher),
            _ => Task.CompletedTask
        };

    private static async Task ExerciseRunLifecycle(IEventPublisher publisher)
    {
        await publisher.PublishAsync(new RunStartedEvent(
            RunId, "ticket", "fix-bug", new[] { "repo1" }, DateTimeOffset.UtcNow));
        await publisher.PublishAsync(new RunFinishedEvent(
            RunId, "success", null, "ok", DateTimeOffset.UtcNow));
    }

    private static async Task ExerciseStepRunner(IEventPublisher publisher)
    {
        await publisher.PublishAsync(new StepStartedEvent(
            RunId, 1, "AnalyzeCode", 10, DateTimeOffset.UtcNow));
        await publisher.PublishAsync(new StepFinishedEvent(
            RunId, 1, "success", 250L, DateTimeOffset.UtcNow));
    }

    private static async Task ExerciseSandboxCoordinator(IEventPublisher publisher)
    {
        await publisher.PublishAsync(new SandboxCreatedEvent(
            RunId, "default", "mcr.microsoft.com/dotnet/sdk:8.0", "csharp", DateTimeOffset.UtcNow));
    }

    private static async Task ExerciseSandboxProjector(IEventPublisher publisher)
    {
        var inner = new RecordingSandbox();
        var projector = new SandboxEventProjector(
            inner, publisher, new ScopedRunContext(RunId), repo: "default");
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "echo", Args: new[] { "hi" }, TimeoutSeconds: 5);
        await projector.RunStepAsync(step, progress: null, CancellationToken.None);
    }

    private static async Task ExerciseLlmDecorator(IEventPublisher publisher)
    {
        var client = new EventPublishingChatClient(
            new StubChat(), publisher, new ScopedRunContext(RunId), role: "Lead");
        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") }, options: null, CancellationToken.None);
    }

    private static async Task ExerciseToolDecorator(IEventPublisher publisher)
    {
        var function = AIFunctionFactory.Create((string path) => "ok", name: "read_file");
        var wrapped = new EventPublishingAIFunction(
            function, publisher, new ScopedRunContext(RunId));
        var args = new AIFunctionArguments { ["path"] = "/work/foo" };
        await wrapped.InvokeAsync(args, CancellationToken.None);
    }

    private static async Task ExerciseDecisionLogger(IEventPublisher publisher)
    {
        var logger = new InMemoryDecisionLogger(
            publisher, new ScopedRunContext(RunId),
            NullLogger<InMemoryDecisionLogger>.Instance);
        await logger.LogAsync(null, DecisionCategory.Architecture, "chose X over Y");
    }

    private static async Task ExerciseGateHandlers(IEventPublisher publisher)
    {
        var handler = new EmptyPlanCheckHandler(publisher, NullLogger<EmptyPlanCheckHandler>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        await handler.ExecuteAsync(new EmptyPlanCheckContext(pipeline), CancellationToken.None);
    }

    private static async Task ExerciseTriageProducer(IEventPublisher publisher)
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        var scorer = new ActivationSpecificityScorer(parser, NullLogger<ActivationSpecificityScorer>.Instance);
        var producer = new TriageOutputProducer(
            new DeterministicTriageSelector(scorer),
            new TriageLabelOverrideApplier(),
            new ActivationSkillFilter(parser, new ActivationEvaluator(),
                NullLogger<ActivationSkillFilter>.Instance),
            new PhaseSpecificityTrimmer(scorer, NullLogger<PhaseSpecificityTrimmer>.Instance),
            RunStateConceptsTestFactory.Default,
            new LoopLimitsConfig { MaxSkillsPerPhase = 10 },
            publisher,
            NullLogger<TriageOutputProducer>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.AvailableRoles,
            (IReadOnlyList<RoleSkillDefinition>)new[]
            {
                new RoleSkillDefinition { Name = "planner", Description = "planner", Role = "producer" }
            });
        await producer.ProduceAsync(pipeline, CancellationToken.None);
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public IDisposable BeginScope(string id) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }

    private sealed class StubChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class RecordingSandbox : ISandbox
    {
        public string JobId => "test-job";
        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            progress?.Report(new StepEvent(
                StepEvent.CurrentSchemaVersion, step.StepId,
                StepEventKind.Stdout, "hello", DateTimeOffset.UtcNow));
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: null));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public enum ProducerId
{
    PipelineExecutorRun,
    PipelineStepRunner,
    SandboxCoordinator,
    SandboxEventProjector,
    LlmDecorator,
    ToolDecorator,
    DecisionLogger,
    GateHandlers,
    TriageOutputProducer
}
