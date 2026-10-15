using AgentSmith.Contracts.Events;
using AgentSmith.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// <see cref="IRunEventFanout"/> impl that routes broadcaster events into
/// the SignalR groups owned by <see cref="JobsHub"/>. Kept thin so the
/// broadcaster stays decoupled from the hub type.
/// </summary>
public sealed class JobsHubFanout(IHubContext<JobsHub> hub, OverviewAudience audience, RunProjectIndex runs) : IRunEventFanout
{
    // p0246f: a THIN nudge, not the full snapshot. The run data lives in the DB
    // (the projector wrote it before this fires); the dashboard reads it from
    // GET /api/runs and refetches when this nudge names a changed run. Redis is
    // demoted to transport — it no longer carries the authoritative RunSnapshot.
    // 2026-10-15-4a4dc: and to the run's project group. A finished run is forgotten here — the moment the
    // broadcaster drops it from its active set — so the index holds only the runs in flight.
    public async Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken cancellationToken)
    {
        var groups = snapshot.FinishedAt is null
            ? await audience.OfLiveRunAsync(snapshot.RunId, snapshot.Project, cancellationToken)
            : await audience.OfRunAsync(snapshot.RunId, snapshot.Project, cancellationToken);
        await hub.Clients.Groups(groups).SendAsync("RunsChanged", snapshot.RunId, cancellationToken);
        if (snapshot.FinishedAt is not null) runs.Forget(snapshot.RunId);
    }

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

    public Task ToTicketActivityAsync(TicketActivityChangedEvent ticketEvent, CancellationToken cancellationToken) =>
        hub.Clients.Groups(audience.OfProject(ticketEvent.Project)).SendAsync(
            "TicketActivityChanged", ticketEvent.Project, ticketEvent.TicketKey, cancellationToken);
}
