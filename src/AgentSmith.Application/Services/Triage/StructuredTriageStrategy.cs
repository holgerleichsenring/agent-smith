using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// Phase-based triage. Stores the full TriageOutput in the pipeline context,
/// sets CurrentPhase=Plan, emits Plan-phase commands. Review/Final phases are
/// dispatched later by RunReviewPhaseHandler / RunFinalPhaseHandler reading
/// the same TriageOutput — for presets that contain those steps.
/// p0131c-pre: when the active preset is single-phase (no RunReviewPhase /
/// RunFinalPhase steps — true for mad-discussion + legal-analysis), the
/// TriageOutput is collapsed via <see cref="SinglePhaseCollapser"/> so
/// LLM-emitted Review/Final assignments don't get silently dropped.
/// </summary>
public sealed class StructuredTriageStrategy(
    ITriageOutputProducer producer,
    PhaseCommandExpander expander,
    SinglePhaseCollapser singlePhaseCollapser,
    ILogger<StructuredTriageStrategy> logger) : ITriageStrategy
{
    public async Task<CommandResult> ExecuteAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (!HasLoadedSkills(pipeline))
        {
            logger.LogWarning(
                "Triage skipped — no skills loaded (skill catalog likely missing roles_supported, " +
                "see SkillLoader rejection logs above). Pipeline cannot proceed past Plan phase.");
            return CommandResult.Fail("No skills loaded — triage cannot assign roles");
        }

        var triage = await producer.ProduceAsync(pipeline, cancellationToken);
        var pipelineName = ResolvePipelineName(pipeline);

        if (PipelinePresets.IsSinglePhase(pipelineName))
        {
            var collapsed = singlePhaseCollapser.Collapse(triage);
            logger.LogInformation(
                "Triage single-phase collapse for preset '{Preset}': merged Review/Final into Plan",
                pipelineName);
            triage = collapsed;
        }

        pipeline.Set(ContextKeys.TriageOutput, triage);
        pipeline.Set(ContextKeys.CurrentPhase, PipelinePhase.Plan);

        var skillRoundCommandName = PipelinePresets.GetSkillRoundCommandName(pipelineName);
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

    private static string ResolvePipelineName(PipelineContext pipeline) =>
        pipeline.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var resolved)
            && resolved is not null
            ? resolved.PipelineName
            : string.Empty;

    private static bool HasLoadedSkills(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var roles)
        && roles is { Count: > 0 };
}
