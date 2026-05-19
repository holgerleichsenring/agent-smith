using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Detects whether a parsed observation list yields a blocking
/// follow-up SkillRound + applies the ping-pong / chain-depth guards.
/// Returns <c>null</c> when the round may proceed without follow-up.
/// </summary>
public interface IBlockingFollowUpDetector
{
    CommandResult? Detect(
        IReadOnlyList<SkillObservation> parsed,
        string skillName,
        RoleSkillDefinition role,
        IReadOnlyList<RoleSkillDefinition> roles,
        int round,
        string skillRoundCommandName,
        PipelineContext pipeline,
        ILogger logger);
}
