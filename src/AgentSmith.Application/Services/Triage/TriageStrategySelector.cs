using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Picks the triage strategy by pipeline_type. Discussion → legacy LLM-based triage;
/// everything else → phase-based structured triage. Single dispatch point so the
/// existing TriageHandler stays a thin adapter once Step 6 swaps the wiring.
/// </summary>
public sealed class TriageStrategySelector(
    LegacyTriageStrategy legacyStrategy,
    StructuredTriageStrategy structuredStrategy) : ITriageStrategySelector
{
    public ITriageStrategy Select(PipelineType pipelineType) =>
        pipelineType is PipelineType.Discussion
            ? legacyStrategy
            : structuredStrategy;
}
