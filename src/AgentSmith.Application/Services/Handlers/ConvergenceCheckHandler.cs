using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates whether all roles have reached consensus on the plan via
/// structured LLM analysis over the SkillObservations published during the
/// discussion. Missing observations fail loud — every discussion-pipeline
/// skill is required to emit observations (p0123).
/// </summary>
public sealed class ConvergenceCheckHandler(
    PlanConsolidator planConsolidator,
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    ILogger<ConvergenceCheckHandler> logger)
    : ICommandHandler<ConvergenceCheckContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ConvergenceCheckContext context, CancellationToken cancellationToken)
    {
        // Structured/hierarchical pipelines don't use convergence — single call per skill
        if (context.Pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
            && pipelineType is not PipelineType.Discussion)
        {
            return CommandResult.Ok("Structured pipeline — no convergence check needed");
        }

        // Already converged
        if (context.Pipeline.Has(ContextKeys.ConvergenceResult))
            return CommandResult.Ok("Already converged (no-op)");

        if (!context.Pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var observations)
            || observations is null || observations.Count == 0)
        {
            return CommandResult.Fail(
                "Convergence check has no SkillObservations to converge over. "
                + "Every discussion-pipeline skill must emit observations (p0123); "
                + "check that the active skills produced parseable output.");
        }

        return await ExecuteStructuredConvergenceAsync(
            context, observations, cancellationToken);
    }

    private async Task<CommandResult> ExecuteStructuredConvergenceAsync(
        ConvergenceCheckContext context,
        List<SkillObservation> observations,
        CancellationToken cancellationToken)
    {
        var maxRounds = GetMaxRounds(context.Pipeline);
        var currentMaxRound = GetCurrentRound(context.Pipeline);

        // Build observations summary for LLM
        var observationsSummary = string.Join("\n", observations.Select(o =>
            $"[{o.Id}] {o.Role} | {o.Concern} | {o.Severity} | blocking={o.Blocking} | confidence={o.Confidence}\n" +
            $"  {o.Description}\n" +
            (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"  → {o.Suggestion}\n")));

        var activeRoles = observations.Select(o => o.Role).Distinct().ToList();
        var userPrompt = $"""
            ## Active Roles
            {string.Join(", ", activeRoles)}

            ## All Observations
            {observationsSummary}

            Analyze these observations for consensus. Respond with JSON only.
            """;

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Reasoning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Reasoning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, prompts.Get("convergence-system")),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);
        var responseText = response.Text ?? string.Empty;

        var result = ConvergenceResultParser.Parse(responseText, observations, logger);
        if (result is null)
        {
            logger.LogWarning("Failed to parse convergence result, treating as no consensus");
            result = new ConvergenceResult(
                false, observations, [], [],
                observations.Where(o => o.Blocking).ToList(),
                observations.Where(o => !o.Blocking).ToList());
        }

        context.Pipeline.Set(ContextKeys.ConvergenceResult, result);

        if (!result.Consensus && currentMaxRound < maxRounds)
        {
            logger.LogInformation(
                "No consensus after round {Round}/{MaxRounds} — {Blocking} blocking, {Contradictions} contradictions",
                currentMaxRound, maxRounds,
                result.Blocking.Count,
                result.Links.Count(l => l.Relationship == ObservationRelationship.Contradicts));

            // Remove the ConvergenceResult so next check can re-evaluate
            context.Pipeline.Set(ContextKeys.ConvergenceResult, (object)null!);

            return InsertAdditionalRounds(context.Pipeline, observations, currentMaxRound);
        }

        if (!result.Consensus)
        {
            logger.LogWarning(
                "No consensus after {MaxRounds} rounds, escalating with {Blocking} blocking observations",
                maxRounds, result.Blocking.Count);

            // Also consolidate for backward compat
            await ConsolidateFromObservations(context, observations, escalated: true, cancellationToken);

            return CommandResult.Ok(
                $"No consensus after {maxRounds} rounds. Escalating to human approval.");
        }

        logger.LogInformation(
            "Consensus reached: {Total} observations, {Blocking} blocking, {Links} links",
            result.Observations.Count, result.Blocking.Count, result.Links.Count);

        // Also consolidate for backward compat
        await ConsolidateFromObservations(context, observations, escalated: false, cancellationToken);

        return CommandResult.Ok($"Consensus reached after {currentMaxRound} round(s)");
    }

    private CommandResult InsertAdditionalRounds(
        PipelineContext pipeline,
        List<SkillObservation> observations,
        int currentMaxRound)
    {
        var blockingRoles = observations
            .Where(o => o.Blocking)
            .Select(o => o.Role)
            .Distinct()
            .ToList();

        var commandsToInsert = new List<PipelineCommand>();
        var nextRound = currentMaxRound + 1;
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var skillRoundCmd);
        var cmdName = skillRoundCmd ?? CommandNames.SkillRound;

        foreach (var role in blockingRoles)
        {
            commandsToInsert.Add(PipelineCommand.SkillRound(cmdName, role, nextRound));
        }

        commandsToInsert.Add(PipelineCommand.Simple(CommandNames.ConvergenceCheck));

        return CommandResult.OkAndContinueWith(
            $"Blocking observations from: {string.Join(", ", blockingRoles)}. Round {nextRound}.",
            commandsToInsert.ToArray());
    }

    private async Task ConsolidateFromObservations(
        ConvergenceCheckContext context,
        List<SkillObservation> observations,
        bool escalated,
        CancellationToken cancellationToken)
    {
        // Build a discussion log from observations for PlanConsolidator backward compat
        if (context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) && discussionLog is not null)
        {
            try
            {
                await planConsolidator.ConsolidateAsync(context, discussionLog, escalated, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consolidation failed — discussion pipeline results may be incomplete");
                throw;
            }
        }
    }

    private static int GetMaxRounds(PipelineContext pipeline)
    {
        var maxRounds = 3;
        if (pipeline.TryGet<SkillConfig>(ContextKeys.ProjectSkills, out var skillConfig)
            && skillConfig is not null)
        {
            maxRounds = skillConfig.Discussion.MaxRounds;
        }
        return maxRounds;
    }

    private static int GetCurrentRound(PipelineContext pipeline)
    {
        if (pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog)
            && discussionLog is not null && discussionLog.Count > 0)
        {
            return discussionLog.Max(e => e.Round);
        }
        return 1;
    }
}
