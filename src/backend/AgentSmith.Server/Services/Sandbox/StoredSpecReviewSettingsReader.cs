using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-13-8762g: the server's spec review settings, read per call from the config store's assembled
/// document — a Studio save applies to the next run without a restart. Not through the configuration loader:
/// that one answers a store failure with an empty configuration, which would review on defaults nobody read.
/// A store without a spec_review document holds the defaults; one that cannot read throws, and so does this.
/// </summary>
public sealed class StoredSpecReviewSettingsReader(IConfigStore store) : ISpecReviewSettingsReader
{
    public SpecReviewSettings Current() => new(
        store.GetSetting(ConfigDocTypes.SpecReview) as SpecReviewConfig
            ?? throw new InvalidOperationException("The stored spec_review settings are not a spec_review block."),
        (store.GetSetting(ConfigDocTypes.Orchestrator) as OrchestratorGlobalConfig
            ?? throw new InvalidOperationException("The stored orchestrator settings are not an orchestrator block."))
            .MaxRunWallTimeSeconds);
}
