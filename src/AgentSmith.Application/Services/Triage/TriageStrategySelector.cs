using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Picks the triage strategy. Hierarchical / Structured presets always go to
/// StructuredTriage. Discussion presets split:
///   - mad-discussion + legal-analysis route to StructuredTriage (their
///     skill catalogs are fully activates_when-tagged post-p0127c, and
///     StructuredTriageStrategy collapses Review/Final phases into Plan
///     when the preset is single-phase).
///   - everything else (autonomous; init-project no longer hits Triage
///     after p0130c; skill-manager has no Triage step) stays on
///     LegacyTriageStrategy. autonomous needs skills with
///     `activates_when="pipeline_name = \"autonomous\""` before it can
///     migrate; that's a separate skills-repo gap.
/// </summary>
public sealed class TriageStrategySelector(
    LegacyTriageStrategy legacyStrategy,
    StructuredTriageStrategy structuredStrategy) : ITriageStrategySelector
{
    private static readonly HashSet<string> StructuredDiscussionPresets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "mad-discussion",
            "legal-analysis",
        };

    public ITriageStrategy Select(PipelineType pipelineType, string pipelineName) =>
        pipelineType is PipelineType.Discussion
            ? StructuredDiscussionPresets.Contains(pipelineName)
                ? structuredStrategy
                : legacyStrategy
            : structuredStrategy;
}
