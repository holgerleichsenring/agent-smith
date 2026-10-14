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

    // 2026-10-13-9a59f: step, spec and master pass, each only when the event does not carry its own.
    private RunEvent Stamp(RunEvent runEvent)
    {
        var step = runEvent.OriginStepIndex ?? runContext.CurrentStepIndex;
        var spec = runEvent.OriginSpecId ?? runContext.CurrentSpecId;
        var pass = runEvent.OriginMasterPass ?? runContext.CurrentMasterPass;
        return step == runEvent.OriginStepIndex && spec == runEvent.OriginSpecId && pass == runEvent.OriginMasterPass
            ? runEvent
            : runEvent with { OriginStepIndex = step, OriginSpecId = spec, OriginMasterPass = pass };
    }
}
