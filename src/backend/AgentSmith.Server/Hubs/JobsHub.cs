using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace AgentSmith.Server.Hubs;

/// <summary>
/// Dashboard fan-out hub. Three Subscribe methods route clients into the
/// overview / per-run / per-sandbox SignalR groups. On SubscribeOverview /
/// SubscribeRun we replay the retained stream window before live tail
/// starts; the MAXLEN=10000 bound is part of the contract — clients see
/// the oldest retained event as the start of their visible history.
/// </summary>
public sealed class JobsHub(
    JobsBroadcaster broadcaster,
    SandboxExpansionRegistry expansionRegistry,
    IConnectionMultiplexer redis) : Hub
{
    public async Task SubscribeOverview()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Overview);
        var snapshot = new
        {
            Active = broadcaster.Active.Values.ToArray(),
            Recent = broadcaster.Recent
        };
        await Clients.Caller.SendAsync("OverviewSnapshot", snapshot);
    }

    public async Task SubscribeRun(string runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Run(runId));
        var db = redis.GetDatabase();
        var entries = await db.StreamRangeAsync(EventStreamKeys.RunStream(runId), "-", "+");
        foreach (var entry in entries)
        {
            foreach (var pair in entry.Values)
            {
                var payload = pair.Value.ToString();
                if (string.IsNullOrEmpty(payload)) continue;
                var runEvent = EventEnvelopeSerializer.Deserialize(payload);
                if (runEvent is null) continue;
                await Clients.Caller.SendAsync("RunEvent", runEvent);
            }
        }
    }

    public async Task ExpandSandbox(string runId, string repo)
    {
        expansionRegistry.Expand(runId, repo);
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Sandbox(runId, repo));
    }

    public async Task CollapseSandbox(string runId, string repo)
    {
        expansionRegistry.Collapse(runId, repo);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Sandbox(runId, repo));
    }
}
