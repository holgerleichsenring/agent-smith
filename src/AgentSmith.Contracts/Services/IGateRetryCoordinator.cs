using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Runs a gate LLM call with one corrective retry. The retry fires only
/// if the first response failed to parse or violated the required schema.
/// </summary>
public interface IGateRetryCoordinator
{
    Task<GateCallOutcome> ExecuteAsync(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string systemPrompt,
        string userPromptPrefix,
        string userPromptSuffix,
        ILlmClient llmClient,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a gate call: the pipeline result plus the final LLM response
/// text (first-attempt text if successful, retry text if retried).
/// </summary>
public sealed record GateCallOutcome(CommandResult Result, string FinalResponseText);
