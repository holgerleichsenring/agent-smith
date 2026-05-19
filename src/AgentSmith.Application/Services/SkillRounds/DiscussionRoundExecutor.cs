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
        int round, ISkillPromptStrategy strategy, PipelineContext pipeline,
        ILogger logger, CancellationToken cancellationToken)
    {
        var (system, userPrefix, userSuffix) = composer.ComposeDiscussion(role, strategy, skillName, round, pipeline);
        var result = await dispatcher.DispatchAsync(
            skillName, role, system, userPrefix, userSuffix, pipeline, cancellationToken);
        if (SkillCallOutcomeTranslator.TranslateDiscussion(result, skillName, role, logger) is { } earlyFail)
            return earlyFail;
        var parsed = responseParser.ParseAndDowngrade(result.Output ?? string.Empty, skillName, logger);
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

    private static void StorePlanArtifactIfPlanLead(
        string skillName, PipelineContext pipeline, IReadOnlyList<SkillObservation> observations)
    {
        if (!pipeline.TryGet<TriageOutput>(ContextKeys.TriageOutput, out var triage) || triage is null) return;
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase) || phase != PipelinePhase.Plan) return;
        if (!triage.Phases.TryGetValue(phase, out var assignment) || assignment.Lead != skillName) return;
        pipeline.Set(ContextKeys.PlanArtifact, new PlanArtifact(skillName, observations, DateTimeOffset.UtcNow));
    }
}
