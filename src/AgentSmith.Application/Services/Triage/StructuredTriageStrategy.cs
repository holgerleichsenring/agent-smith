using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Phase-based triage for fix-bug, add-feature, security-scan, api-scan.
/// Stores the full TriageOutput in the pipeline context, sets CurrentPhase=Plan,
/// and emits Plan-phase commands. Review/Final phases are dispatched later by
/// RunReviewPhaseHandler / RunFinalPhaseHandler reading the same TriageOutput.
/// </summary>
public sealed class StructuredTriageStrategy(
    ITriageOutputProducer producer,
    PhaseCommandExpander expander,
    ILogger<StructuredTriageStrategy> logger) : ITriageStrategy
{
    public async Task<CommandResult> ExecuteAsync(
        PipelineContext pipeline, ILlmClient llmClient, CancellationToken cancellationToken)
    {
        var triage = await producer.ProduceAsync(pipeline, llmClient, cancellationToken);
        pipeline.Set(ContextKeys.TriageOutput, triage);
        pipeline.Set(ContextKeys.CurrentPhase, PipelinePhase.Plan);

        var skillRoundCommandName = ResolveSkillRoundCommandName(pipeline);
        var commands = expander.ExpandPhase(triage, PipelinePhase.Plan, round: 1, skillRoundCommandName);
        if (commands.Count == 0)
        {
            logger.LogInformation("Triage Plan phase has no skills assigned, nothing to dispatch");
            return CommandResult.Ok("Triage: no Plan-phase skills assigned");
        }

        logger.LogInformation(
            "Triage complete (confidence {Confidence}). Plan phase emits {Count} commands",
            triage.Confidence, commands.Count);

        return CommandResult.OkAndContinueWith(
            $"Triage complete (confidence {triage.Confidence}). Plan phase: {commands.Count} commands.",
            commands.ToArray());
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
