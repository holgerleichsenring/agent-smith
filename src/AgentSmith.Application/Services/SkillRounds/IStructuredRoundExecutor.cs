using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Runs a structured (non-discussion) skill round — Lead/Reviewer
/// rounds + the Gate round-trip (delegated to <c>GateRetryCoordinator</c>).
/// Replaces the ~85 lines of <c>ExecuteStructuredRoundAsync</c> + the gate
/// helper on the old base class.
/// </summary>
public interface IStructuredRoundExecutor
{
    Task<CommandResult> ExecuteAsync(
        string skillName,
        RoleSkillDefinition role,
        ISkillPromptStrategy strategy,
        PipelineContext pipeline,
        ILogger logger,
        CancellationToken cancellationToken);
}
