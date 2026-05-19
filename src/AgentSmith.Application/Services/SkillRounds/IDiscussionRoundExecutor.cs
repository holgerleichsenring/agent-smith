using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Runs a discussion-mode skill round end-to-end: compose prompt,
/// dispatch via runtime, parse observations, downgrade by confidence, buffer
/// the discussion entry, detect any blocking follow-up. Replaces the
/// ~65 lines of <c>ExecuteDiscussionRoundAsync</c> on the old base class.
/// </summary>
public interface IDiscussionRoundExecutor
{
    Task<CommandResult> ExecuteAsync(
        string skillName,
        RoleSkillDefinition role,
        IReadOnlyList<RoleSkillDefinition> roles,
        int round,
        ISkillPromptStrategy strategy,
        PipelineContext pipeline,
        ILogger logger,
        CancellationToken cancellationToken);
}
