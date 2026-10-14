using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: the CLI's spec review settings — the configuration file, re-read through the loader on
/// each call when a config path was given (the file loader throws on a failure), otherwise the composed
/// configuration. The server replaces this with a reader over its config store, because the database
/// loader answers a failure with an empty configuration.
/// </summary>
public sealed class LoaderSpecReviewSettingsReader(
    IConfigurationLoader? loader = null,
    ServerContext? context = null,
    AgentSmithConfig? config = null) : ISpecReviewSettingsReader
{
    public SpecReviewSettings Current()
    {
        var current = loader is not null && context is not null
            ? loader.LoadConfig(context.ConfigPath)
            : config ?? AgentSmithConfig.Empty();
        return new SpecReviewSettings(current.SpecReview, current.Orchestrator.MaxRunWallTimeSeconds);
    }
}
