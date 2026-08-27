using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-08-27-3eb1: derives the compaction settings a model role's STATED input
/// window implies. A role that states no window derives nothing — the caller then
/// runs the chain it ran before, so an installation that states no window behaves
/// exactly as it did. No table of model name → window size is consulted: the window
/// is a property of the deployment, and a built-in table is wrong the day a provider
/// ships a variant.
/// </summary>
public sealed class WindowDerivedCompaction
{
    /// <summary>
    /// Returns the compaction config to run against a role with the given window, or
    /// null when nothing can be derived (no window stated, or compaction switched off).
    /// The threshold never exceeds the window; a smaller explicit setting is kept, so
    /// an operator who folds earlier than the window keeps folding earlier.
    /// </summary>
    public CompactionConfig? Derive(CompactionConfig? requested, int? windowTokens)
    {
        if (requested is not { IsEnabled: true }) return null;
        if (windowTokens is not { } window || window <= 0) return null;
        return new CompactionConfig
        {
            IsEnabled = true,
            MaxContextTokens = Math.Min(requested.MaxContextTokens, window),
            MaxContextTokensTriggerRatio = requested.MaxContextTokensTriggerRatio,
            KeepRecentIterations = requested.KeepRecentIterations,
            ThresholdIterations = requested.ThresholdIterations,
            SummaryModel = requested.SummaryModel,
            DeploymentName = requested.DeploymentName,
        };
    }
}
