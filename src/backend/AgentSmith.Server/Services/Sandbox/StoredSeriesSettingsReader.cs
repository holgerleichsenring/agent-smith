using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-13-8762u: the server's series settings, read per call from the config store's assembled document —
/// a Studio save applies to the next run without a restart. A store without a series document holds the
/// defaults; a document of the wrong type throws naming which of the two it was.
/// </summary>
public sealed class StoredSeriesSettingsReader(IConfigStore store) : ISeriesSettingsReader
{
    public SeriesSettings Current() => new(
        store.GetSetting(ConfigDocTypes.Series) as SeriesConfig
            ?? throw new InvalidOperationException("The stored series settings are not a series block."),
        (store.GetSetting(ConfigDocTypes.Orchestrator) as OrchestratorGlobalConfig
            ?? throw new InvalidOperationException("The stored orchestrator settings are not an orchestrator block."))
            .MaxRunWallTimeSeconds);
}
