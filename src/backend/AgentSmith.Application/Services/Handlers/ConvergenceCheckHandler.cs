using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates whether all roles have reached consensus on the plan. The
/// structured LLM call + result parsing is delegated to
/// <see cref="IConvergenceEvaluator"/>; this handler owns pipeline-state
/// gating, round bookkeeping, and the consolidation hand-off.
/// </summary>
public sealed class ConvergenceCheckHandler(
    PlanConsolidator planConsolidator,
    IConvergenceEvaluator evaluator,
    ILogger<ConvergenceCheckHandler> logger)
    : ICommandHandler<ConvergenceCheckContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ConvergenceCheckContext context, CancellationToken cancellationToken)
    {
        var gate = CheckPreconditions(context.Pipeline, out var observations);
        if (gate is not null) return gate;

        var costSink = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var result = await evaluator.EvaluateAsync(
            context.AgentConfig, observations!, costSink.Track, cancellationToken);
        context.Pipeline.Set(ContextKeys.ConvergenceResult, result);

        var maxRounds = GetMaxRounds(context.Pipeline);
        var currentRound = GetCurrentRound(context.Pipeline);
        if (!result.Consensus && currentRound < maxRounds)
        {
            logger.LogInformation(
                "No consensus after round {Round}/{MaxRounds} — {Blocking} blocking, {Contradictions} contradictions",
                currentRound, maxRounds, result.Blocking.Count,
                result.Links.Count(l => l.Relationship == ObservationRelationship.Contradicts));
            context.Pipeline.Set(ContextKeys.ConvergenceResult, (object)null!);
            return InsertAdditionalRounds(context.Pipeline, observations!, currentRound);
        }

        if (!result.Consensus)
        {
            logger.LogWarning(
                "No consensus after {MaxRounds} rounds, escalating with {Blocking} blocking observations",
                maxRounds, result.Blocking.Count);
            await ConsolidateFromObservations(context, observations!, escalated: true, cancellationToken);
            return CommandResult.Ok($"No consensus after {maxRounds} rounds. Escalating to human approval.");
        }

        logger.LogInformation(
            "Consensus reached: {Total} observations, {Blocking} blocking, {Links} links",
            result.Observations.Count, result.Blocking.Count, result.Links.Count);
        await ConsolidateFromObservations(context, observations!, escalated: false, cancellationToken);
        return CommandResult.Ok($"Consensus reached after {currentRound} round(s)");
    }

    private static CommandResult? CheckPreconditions(
        PipelineContext pipeline, out List<SkillObservation>? observations)
    {
        observations = null;
        if (pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is not PipelineType.Discussion)
            return CommandResult.Ok("Structured pipeline — no convergence check needed");
        if (pipeline.Has(ContextKeys.ConvergenceResult))
            return CommandResult.Ok("Already converged (no-op)");
        if (!pipeline.TryGet(ContextKeys.SkillObservations, out observations)
            || observations is null || observations.Count == 0)
            return CommandResult.Fail(
                "Convergence check has no SkillObservations to converge over. "
                + "Every discussion-pipeline skill must emit observations (p0123); "
                + "check that the active skills produced parseable output.");
        return null;
    }

    private static CommandResult InsertAdditionalRounds(
        PipelineContext pipeline, List<SkillObservation> observations, int currentMaxRound)
    {
        var blockingRoles = observations.Where(o => o.Blocking).Select(o => o.Role).Distinct().ToList();
        var nextRound = currentMaxRound + 1;
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var skillRoundCmd);
        var cmdName = skillRoundCmd ?? CommandNames.SkillRound;
        var commands = blockingRoles
            .Select(role => PipelineCommand.SkillRound(cmdName, role, nextRound))
            .Append(PipelineCommand.Simple(CommandNames.ConvergenceCheck))
            .ToArray();
        return CommandResult.OkAndContinueWith(
            $"Blocking observations from: {string.Join(", ", blockingRoles)}. Round {nextRound}.", commands);
    }

    private async Task ConsolidateFromObservations(
        ConvergenceCheckContext context, List<SkillObservation> observations,
        bool escalated, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null) return;
        try { await planConsolidator.ConsolidateAsync(context, discussionLog, escalated, cancellationToken); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Consolidation failed — discussion pipeline results may be incomplete");
            throw;
        }
    }

    private static int GetMaxRounds(PipelineContext pipeline) =>
        pipeline.TryGet<SkillConfig>(ContextKeys.ProjectSkills, out var skillConfig)
        && skillConfig is not null ? skillConfig.Discussion.MaxRounds : 3;

    private static int GetCurrentRound(PipelineContext pipeline) =>
        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussionLog)
        && discussionLog is not null && discussionLog.Count > 0
            ? discussionLog.Max(e => e.Round) : 1;
}
