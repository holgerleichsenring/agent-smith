using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-11-22-747db: StartAsync only starts the broadcaster; its cold start runs after. A case that publishes and
/// then asserts on the drain starts it warm, the way it started before the cold start left StartAsync.
/// </summary>
internal static class JobsBroadcasterStart
{
    public static async Task StartWarmAsync(this JobsBroadcaster broadcaster)
    {
        await broadcaster.StartAsync(CancellationToken.None);
        await broadcaster.Warm.OrHang("the broadcaster's cold start");
    }
}
