using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Strategy that produces the post-triage pipeline-command sequence for a single
/// pipeline run. Selected by ITriageStrategySelector based on PipelineType.
/// LegacyTriageStrategy handles open Discussion runs; StructuredTriageStrategy
/// handles phase-based runs (fix-bug, add-feature, security-scan, api-scan).
/// </summary>
public interface ITriageStrategy
{
    Task<CommandResult> ExecuteAsync(
        PipelineContext pipeline,
        ILlmClient llmClient,
        CancellationToken cancellationToken);
}
