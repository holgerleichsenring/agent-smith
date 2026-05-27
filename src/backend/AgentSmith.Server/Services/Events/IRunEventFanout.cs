using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Decouples <see cref="JobsBroadcaster"/> from the SignalR Hub type.
/// <c>JobsHubFanout</c> implements this against <c>IHubContext&lt;JobsHub&gt;</c>;
/// tests can substitute an in-memory recorder.
/// </summary>
public interface IRunEventFanout
{
    Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken cancellationToken);
    Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken cancellationToken);
    Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken cancellationToken);

    /// <summary>
    /// p0173a: fans a SystemEvent to the HubGroups.System group. Stays on
    /// the run-event fanout interface (instead of a separate ISystemEventFanout)
    /// because the implementation shares the same IHubContext + lifecycle —
    /// splitting would duplicate registration without isolation benefit.
    /// </summary>
    Task ToSystemAsync(SystemEvent systemEvent, CancellationToken cancellationToken);
}
