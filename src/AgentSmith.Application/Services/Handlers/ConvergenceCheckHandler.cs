using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates whether all roles have reached consensus on the plan.
/// On convergence, consolidates the plan via LLM.
/// On failure at max rounds, escalates to human via the approval step.
/// </summary>
public sealed class ConvergenceCheckHandler(
    PlanConsolidator planConsolidator,
    ILogger<ConvergenceCheckHandler> logger)
    : ICommandHandler<ConvergenceCheckContext>
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AgreePattern = new(
        @"AGREE|SUGGESTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<CommandResult> ExecuteAsync(
        ConvergenceCheckContext context, CancellationToken cancellationToken)
    {
        // Structured/hierarchical pipelines don't use convergence — single call per skill
        if (context.Pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is not PipelineType.Discussion)
        {
            return CommandResult.Ok("Structured pipeline — no convergence check needed");
        }

        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
        {
            return CommandResult.Ok("No discussion log, nothing to check");
        }

        // Already converged from a previous check
        if (context.Pipeline.Has(ContextKeys.ConsolidatedPlan))
        {
            return CommandResult.Ok("Already converged (no-op)");
        }

        // Check convergence: look at the last entry per role
        var lastEntryPerRole = discussionLog
            .GroupBy(e => e.RoleName)
            .ToDictionary(g => g.Key, g => g.Last());

        var hasUnresolvedObjections = lastEntryPerRole.Values
            .Any(e => ObjectionPattern.IsMatch(e.Content) && !AgreePattern.IsMatch(e.Content));

        // Get discussion config for max rounds
        var maxRounds = 3;
        if (context.Pipeline.TryGet<SkillConfig>(ContextKeys.ProjectSkills, out var skillConfig)
            && skillConfig is not null)
        {
            maxRounds = skillConfig.Discussion.MaxRounds;
        }

        var currentMaxRound = discussionLog.Max(e => e.Round);

        if (hasUnresolvedObjections && currentMaxRound < maxRounds)
        {
            // Not converged, but still under max rounds -> insert more rounds
            logger.LogInformation(
                "Unresolved objections after round {Round}/{MaxRounds}, continuing discussion",
                currentMaxRound, maxRounds);

            var objectors = lastEntryPerRole
                .Where(kv => ObjectionPattern.IsMatch(kv.Value.Content))
                .Select(kv => kv.Key)
                .ToList();

            var commandsToInsert = new List<PipelineCommand>();
            var nextRound = currentMaxRound + 1;
            context.Pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var skillRoundCmd);
            var cmdName = skillRoundCmd ?? CommandNames.SkillRound;

            foreach (var objector in objectors)
            {
                commandsToInsert.Add(PipelineCommand.SkillRound(cmdName, objector, nextRound));
            }

            commandsToInsert.Add(PipelineCommand.Simple(CommandNames.ConvergenceCheck));

            return CommandResult.OkAndContinueWith(
                $"Unresolved objections from: {string.Join(", ", objectors)}. Round {nextRound}.",
                commandsToInsert.ToArray());
        }

        if (hasUnresolvedObjections)
        {
            // Max rounds reached with unresolved objections -> escalate
            logger.LogWarning(
                "No consensus after {MaxRounds} rounds, escalating to human approval",
                maxRounds);

            // Still consolidate with dissent noted
            await planConsolidator.ConsolidateAsync(context, discussionLog, escalated: true, cancellationToken);

            return CommandResult.Ok(
                $"No consensus after {maxRounds} rounds. Escalating to human approval.");
        }

        // Converged -> consolidate plan
        logger.LogInformation("Consensus reached after {Rounds} rounds", currentMaxRound);
        await planConsolidator.ConsolidateAsync(context, discussionLog, escalated: false, cancellationToken);

        return CommandResult.Ok($"Consensus reached after {currentMaxRound} round(s)");
    }
}
