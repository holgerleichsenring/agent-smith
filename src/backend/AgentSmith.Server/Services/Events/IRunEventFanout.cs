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
    /// p0367: pushes a coalesced <see cref="SandboxActivityRollup"/> to the Run
    /// group on the distinct "SandboxActivity" message — the liveness beat that
    /// replaces the per-tool-call firehose without polluting the lifecycle
    /// "RunEvent" stream.
    /// </summary>
    Task ToRunActivityAsync(string runId, SandboxActivityRollup rollup, CancellationToken cancellationToken);

    /// <summary>
    /// p0173a: fans a SystemEvent to the HubGroups.System group. Stays on
    /// the run-event fanout interface (instead of a separate ISystemEventFanout)
    /// because the implementation shares the same IHubContext + lifecycle —
    /// splitting would duplicate registration without isolation benefit.
    /// </summary>
    Task ToSystemAsync(SystemEvent systemEvent, CancellationToken cancellationToken);

    /// <summary>
    /// p0175-fix: pushes a recomputed <see cref="SystemActivitySnapshot"/> to
    /// the Overview group so /system KPI cards stay in sync with the server's
    /// 24h rollup instead of deriving from the capped client buffer.
    /// </summary>
    Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken cancellationToken);
}
