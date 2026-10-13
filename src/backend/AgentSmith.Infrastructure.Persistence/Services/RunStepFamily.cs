using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the steps-and-calls family — the rows a run event INSERTS by its trail position:
/// steps, model calls and logged decisions.
/// </summary>
public sealed class RunStepFamily(
    RunStepProjection steps, RunLlmCallProjection llmCalls, RunDecisionProjection decisions) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(StepStartedEvent), typeof(StepFinishedEvent), typeof(LlmCallFinishedEvent), typeof(DecisionLoggedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        StepStartedEvent e => steps.StartAsync(uow, e, eventSeq, ct),
        StepFinishedEvent e => steps.FinishAsync(uow, e, eventSeq, ct),
        LlmCallFinishedEvent e => llmCalls.ApplyAsync(uow, e, eventSeq, ct),
        DecisionLoggedEvent e => decisions.ApplyAsync(uow, e, eventSeq, ct),
        _ => Task.CompletedTask,
    };
}
