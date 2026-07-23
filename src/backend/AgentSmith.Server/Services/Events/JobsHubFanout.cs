using AgentSmith.Contracts.Events;
using AgentSmith.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// <see cref="IRunEventFanout"/> impl that routes broadcaster events into
/// the SignalR groups owned by <see cref="JobsHub"/>. Kept thin so the
/// broadcaster stays decoupled from the hub type.
/// </summary>
public sealed class JobsHubFanout(IHubContext<JobsHub> hub) : IRunEventFanout
{
    // p0246f: a THIN nudge, not the full snapshot. The run data lives in the DB
    // (the projector wrote it before this fires); the dashboard reads it from
    // GET /api/runs and refetches when this nudge names a changed run. Redis is
    // demoted to transport — it no longer carries the authoritative RunSnapshot.
    public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("RunsChanged", snapshot.RunId, cancellationToken);

    public Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Run(runId)).SendAsync("RunEvent", runEvent, cancellationToken);

    public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Sandbox(runId, repo)).SendAsync("SandboxEvent", runEvent, cancellationToken);

    public Task ToRunActivityAsync(string runId, SandboxActivityRollup rollup, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Run(runId)).SendAsync("SandboxActivity", rollup, cancellationToken);

    public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.System).SendAsync("SystemEvent", systemEvent, cancellationToken);

    public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("SystemActivityUpdated", snapshot, cancellationToken);
}
