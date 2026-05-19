using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Detects blocking follow-up SkillRound commands from a parsed
/// observation list. Applies the chain-depth cap (Discussion.MaxRounds) and the
/// O(1) immediate-ping-pong guard via the SwitchSkillLastSummoner map.
/// </summary>
public sealed class BlockingFollowUpDetector : IBlockingFollowUpDetector
{
    public CommandResult? Detect(
        IReadOnlyList<SkillObservation> parsed, string skillName,
        RoleSkillDefinition role, IReadOnlyList<RoleSkillDefinition> roles, int round,
        string skillRoundCommandName, PipelineContext pipeline, ILogger logger)
    {
        var blocking = parsed.FirstOrDefault(o => o.Blocking);
        if (blocking is null) return null;

        var targetRole = ResolveTargetRole(roles, skillName, blocking.Concern.ToString());
        if (targetRole is null) return null;

        var maxRounds = ResolveMaxRounds(pipeline);
        if (round >= maxRounds)
        {
            logger.LogInformation(
                "{Skill} blocking observation '{Concern}' would request {Target}, but skill-round cap reached (round {Round} >= max {Max}). Suppressing follow-up.",
                skillName, blocking.Concern, targetRole.Name, round, maxRounds);
            return null;
        }

        if (IsImmediatePingPong(pipeline, targetRole.Name, skillName))
        {
            logger.LogInformation(
                "{Skill} blocking observation '{Concern}' would request {Target}, but {Target} just requested {Skill} in the previous round. Suppressing immediate ping-pong.",
                skillName, blocking.Concern, targetRole.Name, targetRole.Name, skillName);
            return null;
        }

        RecordSwitchSkillSummoner(pipeline, skillName, targetRole.Name);
        var nextRound = round + 1;
        return CommandResult.OkAndContinueWith(
            $"{role.DisplayName} has blocking concern ({blocking.Concern}), requesting {targetRole.DisplayName}",
            PipelineCommand.SkillRound(skillRoundCommandName, targetRole.Name, nextRound),
            PipelineCommand.SkillRound(skillRoundCommandName, skillName, nextRound),
            PipelineCommand.Simple(CommandNames.ConvergenceCheck));
    }

    private static RoleSkillDefinition? ResolveTargetRole(
        IReadOnlyList<RoleSkillDefinition> roles, string skillName, string concern) =>
        roles.FirstOrDefault(r =>
            r.Name != skillName &&
            r.Triggers.Any(t => t.Contains(concern, StringComparison.OrdinalIgnoreCase)));

    private static int ResolveMaxRounds(PipelineContext pipeline) =>
        pipeline.TryGet<SkillConfig>(ContextKeys.ProjectSkills, out var cfg) && cfg is not null
            ? cfg.Discussion.MaxRounds : 3;

    private static bool IsImmediatePingPong(PipelineContext pipeline, string proposedTarget, string currentSkill)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SwitchSkillLastSummoner, out var summoners) || summoners is null)
            return false;
        return summoners.TryGetValue(currentSkill, out var summoner)
            && summoner.Equals(proposedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static void RecordSwitchSkillSummoner(PipelineContext pipeline, string summoner, string summoned)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SwitchSkillLastSummoner, out var summoners) || summoners is null)
            summoners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        summoners[summoned] = summoner;
        pipeline.Set(ContextKeys.SwitchSkillLastSummoner, summoners);
    }
}
