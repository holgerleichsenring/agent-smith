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
}
