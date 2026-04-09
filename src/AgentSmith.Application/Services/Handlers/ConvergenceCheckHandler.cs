using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates whether all roles have reached consensus on the plan.
/// On convergence, consolidates the plan via LLM.
/// On failure at max rounds, escalates to human via the approval step.
/// </summary>
public sealed class ConvergenceCheckHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<ConvergenceCheckHandler> logger)
    : ICommandHandler<ConvergenceCheckContext>
{
    private const string AssessmentJsonExample =
        """{ "summary": "1. ...", "assessments": [{ "file": "src/Foo.cs", "line": 42, "title": "...", "status": "confirmed", "reason": "..." }] }""";

    private static readonly Regex ObjectionPattern = new(
        @"OBJECTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AgreePattern = new(
        @"AGREE|SUGGESTION",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<CommandResult> ExecuteAsync(
        ConvergenceCheckContext context, CancellationToken cancellationToken)
    {
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
            await ConsolidatePlanAsync(context, discussionLog, escalated: true, cancellationToken);

            return CommandResult.Ok(
                $"No consensus after {maxRounds} rounds. Escalating to human approval.");
        }

        // Converged -> consolidate plan
        logger.LogInformation("Consensus reached after {Rounds} rounds", currentMaxRound);
        await ConsolidatePlanAsync(context, discussionLog, escalated: false, cancellationToken);

        return CommandResult.Ok($"Consensus reached after {currentMaxRound} round(s)");
    }

    internal static (string Summary, List<FindingAssessment> Assessments) ParseConsolidationResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<ConsolidationResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed is not null)
                {
                    var assessments = (parsed.Assessments ?? [])
                        .Where(a => a.Status is "confirmed" or "false_positive")
                        .Select(a => new FindingAssessment(
                            a.File ?? "", a.Line, a.Title ?? "", a.Status ?? "confirmed", a.Reason ?? ""))
                        .ToList();

                    return (parsed.Summary ?? response, assessments);
                }
            }
        }
        catch
        {
            // JSON parsing failed — fall back to raw text as summary
        }

        return (response, []);
    }

    private sealed class ConsolidationResponse
    {
        public string? Summary { get; set; }
        public List<AssessmentEntry>? Assessments { get; set; }
    }

    private sealed class AssessmentEntry
    {
        public string? File { get; set; }
        public int Line { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
    }

    private async Task ConsolidatePlanAsync(
        ConvergenceCheckContext context,
        List<DiscussionEntry> discussionLog,
        bool escalated,
        CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);

        var discussionText = string.Join("\n\n---\n\n", discussionLog.Select(e =>
            $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"));

        context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket);

        var escalationNote = escalated
            ? "\nNOTE: Not all roles agreed. Note the dissenting views in the summary."
            : "";

        var systemPrompt = "You are consolidating a multi-specialist discussion into a final summary.";

        var inputSection = ticket is not null
            ? $"""
                ## Ticket
                {ticket.Title}
                {ticket.Description}
                """
            : "## Analysis Target\nSee discussion below for context.";

        var userPrompt = $"""
            {inputSection}

            ## Discussion
            {discussionText}
            {escalationNote}

            ## Task
            Create a consolidated summary that incorporates all findings and agreed-upon decisions.

            Respond with a JSON object containing:
            1. "summary" — a numbered list of concrete findings and recommendations (string)
            2. "assessments" — an array of finding assessments, one per finding discussed.
               Each assessment has: "file" (string), "line" (int), "title" (string),
               "status" ("confirmed" or "false_positive"), "reason" (brief explanation).

            Only include findings that were explicitly discussed. Findings not listed
            are treated as not_reviewed (they are NOT filtered out).

            Example:
            {AssessmentJsonExample}
            """;

        try
        {
            var llmResponse = await llmClient.CompleteAsync(
                systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
            PipelineCostTracker.GetOrCreate(context.Pipeline).Track(llmResponse);

            var (consolidatedPlan, assessments) = ParseConsolidationResponse(llmResponse.Text);

            context.Pipeline.Set(ContextKeys.ConsolidatedPlan, consolidatedPlan);

            if (assessments.Count > 0)
            {
                context.Pipeline.Set(ContextKeys.FindingAssessments, assessments);
                logger.LogInformation(
                    "Parsed {Count} finding assessments ({Confirmed} confirmed, {FP} false_positive)",
                    assessments.Count,
                    assessments.Count(a => a.Status == "confirmed"),
                    assessments.Count(a => a.Status == "false_positive"));
            }

            // Also set as the Plan so GeneratePlan can be skipped
            var steps = consolidatedPlan.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select((l, i) => new PlanStep(i + 1, l.TrimStart('-', ' ', '*'), null, "modify"))
                .ToList();
            var title = ticket?.Title ?? "Security Scan Findings";
            var plan = new Plan(title, steps, consolidatedPlan);
            context.Pipeline.Set(ContextKeys.Plan, plan);

            logger.LogInformation("Consolidated plan stored ({Chars} chars)", consolidatedPlan.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to consolidate plan, discussion log preserved");
        }
    }
}
