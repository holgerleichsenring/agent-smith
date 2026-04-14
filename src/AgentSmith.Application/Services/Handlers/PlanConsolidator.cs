using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Consolidates a multi-role discussion into a final plan via LLM.
/// </summary>
public sealed class PlanConsolidator(
    ILlmClientFactory llmClientFactory,
    ILogger<PlanConsolidator> logger)
{
    private const string AssessmentJsonExample =
        """{ "summary": "1. ...", "assessments": [{ "file": "src/Foo.cs", "line": 42, "title": "...", "status": "confirmed", "reason": "..." }] }""";

    public async Task ConsolidateAsync(
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

            var (consolidatedPlan, assessments) = ConsolidationResponseParser.Parse(llmResponse.Text);

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
