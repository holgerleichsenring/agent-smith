using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762u: the CLI's series settings — the configuration file, re-read through the loader on each
/// call when a config path was given (the file loader throws on a failure), otherwise the composed
/// configuration. The server replaces this with a reader over its config store.
/// </summary>
public sealed class LoaderSeriesSettingsReader(
    IConfigurationLoader? loader = null,
    ServerContext? context = null,
    AgentSmithConfig? config = null) : ISeriesSettingsReader
{
    public SeriesSettings Current()
    {
        var current = loader is not null && context is not null
            ? loader.LoadConfig(context.ConfigPath)
            : config ?? AgentSmithConfig.Empty();
        return new SeriesSettings(current.Series, current.Orchestrator.MaxRunWallTimeSeconds);
    }
}
