using AgentSmith.Contracts.Services;
using AgentSmith.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-09-20-9f00: the hub-backed <see cref="IRunListNudge"/> — the same door the delete
/// endpoint already knocks on, and the same message <see cref="JobsHubFanout"/> sends when a
/// run event reaches the overview group. Registered inside the dashboard-API composition
/// because that is what adds SignalR; the shared composition binds the no-op.
/// </summary>
public sealed class JobsHubRunListNudge(IHubContext<JobsHub> hub) : IRunListNudge
{
    public Task RunsChangedAsync(string runId, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("RunsChanged", runId, cancellationToken);
}
