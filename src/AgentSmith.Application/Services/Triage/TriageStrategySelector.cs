using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0131c: collapsed to a one-liner returning <see cref="StructuredTriageStrategy"/>
/// for every (PipelineType, pipelineName) pair. LegacyTriageStrategy retired
/// — every preset that runs a Triage step has full activates_when coverage
/// post-p0127c and is handled by StructuredTriageStrategy's single-phase
/// collapse path (added in p0131c-pre) when the preset lacks
/// RunReviewPhase / RunFinalPhase steps. Selector retained as a DI seam so
/// future routing decisions (e.g. provider-specific triage variants) plug
/// in without re-wiring callers.
/// </summary>
public sealed class TriageStrategySelector(
    StructuredTriageStrategy structuredStrategy) : ITriageStrategySelector
{
    public ITriageStrategy Select(PipelineType pipelineType, string pipelineName) => structuredStrategy;
}
