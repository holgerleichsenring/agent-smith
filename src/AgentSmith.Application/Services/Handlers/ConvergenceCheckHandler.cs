using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates whether all roles have reached consensus on the plan.
/// When SkillObservations are present, produces a ConvergenceResult via structured LLM analysis.
/// Falls back to legacy OBJECTION/AGREE pattern matching on DiscussionLog.
/// </summary>
public sealed class ConvergenceCheckHandler(
    PlanConsolidator planConsolidator,
    ILlmClientFactory llmClientFactory,
    ILogger<ConvergenceCheckHandler> logger)
    : ICommandHandler<ConvergenceCheckContext>
{
    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AgreePattern = new(
        @"AGREE|SUGGESTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal const string ConvergenceSystemPrompt = """
        You are analyzing a set of typed observations from multiple specialist agents to determine consensus.

        Your job:
        1. Identify relationships between observations (duplicates, contradictions, dependencies, extensions).
        2. Determine if additional specialist roles are needed for uncovered concern areas.
        3. Assess overall consensus.

        Respond with ONLY a JSON object:
        {
          "consensus": true/false,
          "links": [
            {
              "observationId": <int>,
              "relatedObservationId": <int>,
              "relationship": "duplicates" | "contradicts" | "dependsOn" | "extends"
            }
          ],
          "additionalRoles": ["role_name"]
        }

        Rules:
        - consensus = true when blocking observations do not contradict each other
        - consensus = false when any blocking observations with the same concern area contradict
        - Only suggest additionalRoles if a concern area has observations but no active role covers it
        - Do NOT repeat observations — only produce links and consensus assessment
        """;

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

        // New path: structured observations
        if (context.Pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var observations)
            && observations is not null && observations.Count > 0)
        {
            return await ExecuteStructuredConvergenceAsync(
                context, observations, cancellationToken);
        }

        // Legacy path: free-text discussion log
        return await ExecuteLegacyConvergenceAsync(context, cancellationToken);
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

        var llmClient = llmClientFactory.Create(context.AgentConfig);
        var llmResponse = await llmClient.CompleteAsync(
            ConvergenceSystemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(llmResponse);

        var result = ConvergenceResultParser.Parse(llmResponse.Text, observations, logger);
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

    private async Task<CommandResult> ExecuteLegacyConvergenceAsync(
        ConvergenceCheckContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var discussionLog) || discussionLog is null)
        {
            return CommandResult.Ok("No discussion log, nothing to check");
        }

        if (context.Pipeline.Has(ContextKeys.ConsolidatedPlan))
            return CommandResult.Ok("Already converged (no-op)");

        var lastEntryPerRole = discussionLog
            .GroupBy(e => e.RoleName)
            .ToDictionary(g => g.Key, g => g.Last());

        var hasUnresolvedObjections = lastEntryPerRole.Values
            .Any(e => ObjectionPattern.IsMatch(e.Content) && !AgreePattern.IsMatch(e.Content));

        var maxRounds = GetMaxRounds(context.Pipeline);
        var currentMaxRound = discussionLog.Max(e => e.Round);

        if (hasUnresolvedObjections && currentMaxRound < maxRounds)
        {
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
            logger.LogWarning(
                "No consensus after {MaxRounds} rounds, escalating to human approval",
                maxRounds);

            await planConsolidator.ConsolidateAsync(context, discussionLog, escalated: true, cancellationToken);

            return CommandResult.Ok(
                $"No consensus after {maxRounds} rounds. Escalating to human approval.");
        }

        logger.LogInformation("Consensus reached after {Rounds} rounds", currentMaxRound);
        await planConsolidator.ConsolidateAsync(context, discussionLog, escalated: false, cancellationToken);

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
