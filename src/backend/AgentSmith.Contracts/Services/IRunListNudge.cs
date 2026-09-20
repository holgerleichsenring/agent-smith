namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-20-9f00: tells the dashboards watching the run list that a run row changed, so
/// they refetch it. The surface has no polling interval — it refetches on this nudge, on
/// reconnect and on mount — so a row written outside the run-event fanout stays invisible
/// until something unrelated happens to nudge it.
/// <para>
/// A port because the only door to the surface is the SignalR hub, which lives in the server
/// assembly; the application layer that writes a capacity-deferred run row cannot reference
/// it. Publishing a run event would not do instead: a deferred run was never in the active
/// set, so no cursor is created for its stream and nothing drains it.
/// </para>
/// <para>
/// The no-op is the default binding, so a composition without the dashboard API — which is
/// what adds the hub — still resolves every caller.
/// </para>
/// </summary>
public interface IRunListNudge
{
    /// <summary>
    /// Data-free by design: the run id names what changed and the surface reads the row
    /// itself. Reaches the clients connected to THIS replica — the hub has no backplane.
    /// </summary>
    Task RunsChangedAsync(string runId, CancellationToken cancellationToken);
}
