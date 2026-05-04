using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Handles RunReviewPhase / RunFinalPhase commands by re-reading the stored TriageOutput,
/// updating CurrentPhase, and emitting the SkillRound/FilterRound commands the phase
/// requires. Skipped silently when no TriageOutput is present (e.g. discussion-pipeline runs).
/// </summary>
public sealed class PhaseAdvanceHandler(
    PhaseCommandExpander expander,
    ILogger<PhaseAdvanceHandler> logger) : ICommandHandler<PhaseAdvanceContext>
{
    public Task<CommandResult> ExecuteAsync(
        PhaseAdvanceContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<TriageOutput>(ContextKeys.TriageOutput, out var triage) || triage is null)
        {
            logger.LogInformation("{Phase} phase skipped — no TriageOutput in context", context.Phase);
            return Task.FromResult(CommandResult.Ok($"{context.Phase} phase: no triage output, skipping"));
        }

        pipeline.Set(ContextKeys.CurrentPhase, context.Phase);
        var skillRoundCommandName = ResolveSkillRoundCommandName(pipeline);
        var commands = expander.ExpandPhase(triage, context.Phase, context.Round, skillRoundCommandName);
        if (commands.Count == 0)
        {
            logger.LogInformation("{Phase} phase: no skills assigned", context.Phase);
            return Task.FromResult(CommandResult.Ok($"{context.Phase} phase: no skills assigned"));
        }

        logger.LogInformation("{Phase} phase emits {Count} commands", context.Phase, commands.Count);
        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"{context.Phase} phase: {commands.Count} commands.", commands.ToArray()));
    }

    private static string ResolveSkillRoundCommandName(PipelineContext pipeline)
    {
        var pipelineName = pipeline.TryGet<ResolvedPipelineConfig>(
            ContextKeys.ResolvedPipeline, out var resolved) && resolved is not null
            ? resolved.PipelineName
            : string.Empty;
        return PipelinePresets.GetSkillRoundCommandName(pipelineName);
    }
}
