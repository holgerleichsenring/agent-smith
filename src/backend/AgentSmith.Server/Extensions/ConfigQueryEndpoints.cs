using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
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
        // p0345c: the loader's LastRead + the file's mtime ride along as drift
        // facts (configPath / fileModifiedAt / lastReadAt) for the config-reads
        // story — "is what runs what you configured".
        app.MapGet("/api/config", (AgentSmithConfig config, IConfigResolver resolver, IConfigurationLoader loader) =>
            Results.Ok(ConfigSnapshotMapper.ToSnapshot(config, resolver, loader.LastRead)));
        return app;
    }
}
