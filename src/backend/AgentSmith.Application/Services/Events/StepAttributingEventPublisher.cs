using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// p0388a: stamps the ambient step index onto every published event, centrally,
/// on the one path every producer already goes through. Emit sites stay
/// unchanged — step membership is read from <see cref="IRunContextAccessor"/>,
/// which PipelineStepRunner scopes per step, so sub-agent and LLM events raised
/// on child async flows inherit their spawning step. An event that already
/// carries an attribution keeps it; outside any step the stamp stays null.
/// </summary>
public sealed class StepAttributingEventPublisher(
    IEventPublisher inner,
    IRunContextAccessor runContext) : IEventPublisher
{
    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        inner.PublishAsync(Stamp(runEvent), cancellationToken);

    private RunEvent Stamp(RunEvent runEvent) =>
        runEvent.OriginStepIndex is not null || runContext.CurrentStepIndex is null
            ? runEvent
            : runEvent with { OriginStepIndex = runContext.CurrentStepIndex };
}
