using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0367: the persistence seam for drained run events, split out of the fan-out
/// path. Tool-call events (SandboxCommand/SandboxResult) never drove the UI, so
/// their write must not ride the broadcast hot path — the router persists them
/// here (batched, off the Run-group send) while the fan-out stays pure transport.
/// </summary>
public interface IRunEventPersistence
{
    Task PersistAsync(RunEvent runEvent, CancellationToken cancellationToken);
}
