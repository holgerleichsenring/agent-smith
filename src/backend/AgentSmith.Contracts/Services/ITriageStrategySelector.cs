using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves the ITriageStrategy implementation for a given (PipelineType,
/// pipelineName) pair. p0131c-pre: signature gained the name parameter so
/// Discussion-type presets can route per-name — mad-discussion +
/// legal-analysis use StructuredTriage with single-phase collapse,
/// autonomous remains on LegacyTriage until its skill-set lands.
/// </summary>
public interface ITriageStrategySelector
{
    ITriageStrategy Select(PipelineType pipelineType, string pipelineName);
}
