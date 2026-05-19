using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Decides which LLM-callable tools a given skill round may bind. Distinct from
/// <see cref="IPipelineToolPolicy"/> (which is a per-pipeline allow-list over
/// IToolHost types): a round-tool-policy returns the concrete tool list for a
/// single dispatch, taking into account the skill's role metadata (e.g.
/// investigator_mode) and per-run state (e.g. ActiveMode for api-security).
/// Three concrete implementations land in p0148: Discussion / Structured /
/// Filter — each routed by its owning round handler.
/// </summary>
public interface ISkillRoundToolPolicy
{
    /// <summary>
    /// Returns the tools the skill call may bind. Empty list = single-shot
    /// prompt with no tool access. Implementations read sandbox + pipeline
    /// preset name from <paramref name="pipeline"/> as needed.
    /// </summary>
    IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline);
}
