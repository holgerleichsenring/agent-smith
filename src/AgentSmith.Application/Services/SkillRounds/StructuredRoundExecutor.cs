using AgentSmith.Application.Models;
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
/// p0147d: Structured (non-discussion) round executor. Handles Lead/Reviewer
/// rounds via the standard runtime dispatch and Gate rounds via the dedicated
/// <see cref="IGateRetryCoordinator"/> (gate keeps its own retry policy per
/// p0142 — runtime + gate retry have different semantics).
/// </summary>
public sealed class StructuredRoundExecutor(
    IPromptComposer composer,
    ISkillRoundDispatcher dispatcher,
    ISkillRoundBufferDispatcher bufferDispatcher,
    IGateRetryCoordinator gateRetryCoordinator) : IStructuredRoundExecutor
{
    public async Task<CommandResult> ExecuteAsync(
        string skillName, RoleSkillDefinition role, ISkillPromptStrategy strategy,
        PipelineContext pipeline, ILogger logger, CancellationToken cancellationToken)
    {
        var orch = role.Orchestration!;
        var (system, userPrefix, userSuffix) = composer.ComposeStructured(role, strategy, pipeline);
        return orch.Role == OrchestrationRole.Gate
            ? await ExecuteGateAsync(skillName, role, orch, system, userPrefix, userSuffix, pipeline, logger, cancellationToken)
            : await ExecuteLeadReviewerAsync(skillName, role, orch, system, userPrefix, userSuffix, pipeline, logger, cancellationToken);
    }

    private async Task<CommandResult> ExecuteLeadReviewerAsync(
        string skillName, RoleSkillDefinition role, SkillOrchestration orch,
        string system, string userPrefix, string userSuffix,
        PipelineContext pipeline, ILogger logger, CancellationToken cancellationToken)
    {
        var result = await dispatcher.DispatchAsync(
            skillName, role, system, userPrefix, userSuffix, pipeline, cancellationToken);
        if (SkillCallOutcomeTranslator.TranslateStructured(result, skillName, role) is { } earlyFail)
            return earlyFail;
        var responseText = result.Output ?? string.Empty;
        logger.LogInformation("{Emoji} {DisplayName} [{Role}]: structured round complete",
            role.Emoji, role.DisplayName, orch.Role);
        bufferDispatcher.Dispatch(pipeline, new SkillRoundBuffer(skillName, 0, [], null, responseText));
        if (orch.Role == OrchestrationRole.Lead)
            pipeline.Set(ContextKeys.ConsolidatedPlan, responseText);
        return CommandResult.Ok($"{role.DisplayName} [{orch.Role}]: complete");
    }

    private async Task<CommandResult> ExecuteGateAsync(
        string skillName, RoleSkillDefinition role, SkillOrchestration orch,
        string system, string userPrefix, string userSuffix,
        PipelineContext pipeline, ILogger logger, CancellationToken cancellationToken)
    {
        // p0142 (closes deferred-3x debt): gate stays direct — GateRetryCoordinator
        // owns the corrective-retry policy. Each attempt records cost inside a
        // SkillCallScope so PerSkillBreakdown reflects per-attempt tokens.
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        GateCallOutcome outcome;
        using (var _ = costTracker.BeginCall(skillName, role.Role ?? "investigator", MapPhase(pipeline)))
        {
            outcome = await gateRetryCoordinator.ExecuteAsync(
                role, orch, system, userPrefix, userSuffix, pipeline, cancellationToken,
                onResponse: costTracker.Track);
        }
        bufferDispatcher.Dispatch(pipeline, new SkillRoundBuffer(skillName, 0, [], null, outcome.FinalResponseText));
        logger.LogInformation("{Emoji} {DisplayName} [Gate]: {Message}",
            role.Emoji, role.DisplayName, outcome.Result.Message);
        return outcome.Result;
    }

    private static SkillExecutionPhase MapPhase(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase))
            return SkillExecutionPhase.Discuss;
        return phase switch
        {
            PipelinePhase.Plan => SkillExecutionPhase.Plan,
            PipelinePhase.Review => SkillExecutionPhase.Review,
            PipelinePhase.Final => SkillExecutionPhase.Synthesize,
            _ => SkillExecutionPhase.Discuss,
        };
    }
}
