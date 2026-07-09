using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Discussion-mode round executor. Composes prompt → dispatches LLM
/// call → parses observations + downgrades by confidence → stores plan
/// artifact when applicable → buffers the discussion entry → detects blocking
/// follow-up. Mirrors the old <c>ExecuteDiscussionRoundAsync</c> behaviour
/// 1:1; only the responsibility-per-class layout changes.
/// </summary>
public sealed class DiscussionRoundExecutor(
    IPromptComposer composer,
    ISkillRoundDispatcher dispatcher,
    ISkillResponseParser responseParser,
    ISkillRoundBufferDispatcher bufferDispatcher,
    IBlockingFollowUpDetector blockingDetector) : IDiscussionRoundExecutor
{
    public async Task<CommandResult> ExecuteAsync(
        string skillName, RoleSkillDefinition role, IReadOnlyList<RoleSkillDefinition> roles,
        int round, ISkillPromptStrategy strategy, ISkillRoundToolPolicy toolPolicy,
        PipelineContext pipeline, ILogger logger, CancellationToken cancellationToken)
    {
        var (system, userPrefix, userSuffix) = composer.ComposeDiscussion(role, strategy, skillName, round, pipeline);
        var result = await dispatcher.DispatchAsync(
            skillName, role, system, userPrefix, userSuffix, toolPolicy, pipeline, cancellationToken);
        if (SkillCallOutcomeTranslator.TranslateDiscussion(result, skillName, role, logger) is { } earlyFail)
            return earlyFail;
        // Empty output is treated as zero observations; the parser's prose-wrap
        // fallback was emitting ghost INFO entries when the runtime skipped the
        // call (cost cap, exec-limit). The reason is already encoded as a typed
        // RuntimeObservation below — surface that instead of a placeholder.
        var parsed = string.IsNullOrWhiteSpace(result.Output)
            ? new List<SkillObservation>()
            : responseParser.ParseAndDowngrade(
                result.Output, skillName, logger, result.ReadPaths, ResolveConfidenceThreshold(pipeline));
        if (result.RuntimeObservations.Count > 0)
            parsed.AddRange(result.RuntimeObservations);
        StorePlanArtifactIfPlanLead(skillName, pipeline, parsed);
        var entry = new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji, round, responseParser.RenderObservationsAsText(parsed));
        bufferDispatcher.Dispatch(pipeline, new SkillRoundBuffer(skillName, round, parsed, entry, null));
        logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): {Count} observations",
            role.Emoji, role.DisplayName, round, parsed.Count);
        return blockingDetector.Detect(
            parsed, skillName, role, roles, round, strategy.SkillRoundCommandName, pipeline, logger)
            ?? CommandResult.Ok($"{role.DisplayName} (Round {round}): {parsed.Count} observations");
    }

    private static int ResolveConfidenceThreshold(PipelineContext pipeline) =>
        pipeline.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var resolved)
            && resolved is not null
            ? resolved.ConfidenceThreshold
            : ResolvedPipelineConfig.DefaultConfidenceThreshold;

    private static void StorePlanArtifactIfPlanLead(
        string skillName, PipelineContext pipeline, IReadOnlyList<SkillObservation> observations)
    {
        if (!pipeline.TryGet<TriageOutput>(ContextKeys.TriageOutput, out var triage) || triage is null) return;
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase) || phase != PipelinePhase.Plan) return;
        if (!triage.Phases.TryGetValue(phase, out var assignment) || assignment.Lead != skillName) return;
        pipeline.Set(ContextKeys.PlanArtifact, new PlanArtifact(skillName, observations, DateTimeOffset.UtcNow));
    }
}
