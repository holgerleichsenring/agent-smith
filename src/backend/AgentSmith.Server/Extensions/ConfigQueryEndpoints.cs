using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Config;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0266: the dashboard's READ surface for the resolved agent-smith config —
/// "how the system is wired" (projects → repos/trackers/agent/pipelines +
/// global defaults), the config-time complement to the per-run topology. Maps
/// the DI <see cref="AgentSmithConfig"/> singleton through
/// <see cref="ConfigSnapshotMapper"/>, which redacts every secret-bearing field.
/// Mapped only inside Program.cs's <c>AGENTSMITH_UI_API_ENABLED</c> block (like
/// the run + catalog endpoints), so a dashboard-less deployment never exposes it.
/// </summary>
internal static class ConfigQueryEndpoints
{
    internal static WebApplication MapConfigQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config", (AgentSmithConfig config) =>
            Results.Ok(ConfigSnapshotMapper.ToSnapshot(config)));
        return app;
    }
}
