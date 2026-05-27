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
    public Task ToOverviewAsync(RunSnapshot snapshot, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("JobUpserted", snapshot, cancellationToken);

    public Task ToRunAsync(string runId, RunEvent runEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Run(runId)).SendAsync("RunEvent", runEvent, cancellationToken);

    public Task ToSandboxAsync(string runId, string repo, RunEvent runEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Sandbox(runId, repo)).SendAsync("SandboxEvent", runEvent, cancellationToken);

    public Task ToSystemAsync(SystemEvent systemEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.System).SendAsync("SystemEvent", systemEvent, cancellationToken);

    public Task ToSystemActivityAsync(SystemActivitySnapshot snapshot, CancellationToken cancellationToken) =>
        hub.Clients.Group(HubGroups.Overview).SendAsync("SystemActivityUpdated", snapshot, cancellationToken);
}
