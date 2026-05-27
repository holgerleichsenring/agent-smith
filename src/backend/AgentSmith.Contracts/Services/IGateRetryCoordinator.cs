using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Runs a gate LLM call with one corrective retry. The retry fires only
/// if the first response failed to parse or violated the required schema.
/// Implementation resolves IChatClient via IChatClientFactory.
/// </summary>
public interface IGateRetryCoordinator
{
    /// <summary>
    /// Runs the gate LLM call (with up to one corrective retry).
    /// <paramref name="onResponse"/> fires once per actual chat attempt
    /// (initial + retry) so callers — typically a SkillCallScope owner —
    /// can attribute per-attempt cost / limit-enforcer counters without
    /// reaching inside the coordinator.
    /// </summary>
    Task<GateCallOutcome> ExecuteAsync(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string systemPrompt,
        string userPromptPrefix,
        string userPromptSuffix,
        PipelineContext pipeline,
        CancellationToken cancellationToken,
        Action<Microsoft.Extensions.AI.ChatResponse>? onResponse = null);
}

/// <summary>
/// Outcome of a gate call: the pipeline result plus the final LLM response
/// text (first-attempt text if successful, retry text if retried).
/// </summary>
public sealed record GateCallOutcome(CommandResult Result, string FinalResponseText);
