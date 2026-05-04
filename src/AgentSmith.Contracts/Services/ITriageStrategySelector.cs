using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves the ITriageStrategy implementation for a given PipelineType.
/// Single dispatch point so the existing TriageHandler stays a thin adapter
/// once Step 6 swaps the wiring.
/// </summary>
public interface ITriageStrategySelector
{
    ITriageStrategy Select(PipelineType pipelineType);
}
