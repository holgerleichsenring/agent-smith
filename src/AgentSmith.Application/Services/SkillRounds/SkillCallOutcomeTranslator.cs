using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Maps a <see cref="SkillCallResult"/> outcome to a short-circuit
/// <see cref="CommandResult"/> per the p0142 round-type policies.
/// Discussion: Ok + Incomplete are acceptable; everything else fails.
/// Structured: only Ok proceeds (downstream gate-handler rejects partials).
/// Returns null when the round may proceed.
/// </summary>
public static class SkillCallOutcomeTranslator
{
    public static CommandResult? TranslateDiscussion(
        SkillCallResult result, string skillName, RoleSkillDefinition role, ILogger logger)
    {
        switch (result.Outcome)
        {
            case SkillCallOutcome.Ok:
                return null;
            case SkillCallOutcome.Incomplete:
                logger.LogWarning(
                    "{Skill} ({Role}) discussion round returned Incomplete (limit: {Limit}) — partial observations will be used",
                    skillName, role.DisplayName, result.Cost.HitLimit ?? "unknown");
                return null;
            default:
                return CommandResult.Fail(
                    $"{role.DisplayName} ({skillName}): {result.Outcome} — {result.FailureReason ?? "no reason given"}");
        }
    }

    public static CommandResult? TranslateStructured(
        SkillCallResult result, string skillName, RoleSkillDefinition role) =>
        result.Outcome == SkillCallOutcome.Ok
            ? null
            : CommandResult.Fail(
                $"{role.DisplayName} ({skillName}): {result.Outcome} — {result.FailureReason ?? "no reason given"}");
}
