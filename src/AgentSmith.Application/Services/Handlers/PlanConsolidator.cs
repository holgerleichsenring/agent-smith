using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Consolidates a multi-role discussion into structured findings via LLM.
/// Pipeline-agnostic: title and role come from the caller via ConsolidationRequest.
/// </summary>
public sealed class PlanConsolidator(
    ILlmClientFactory llmClientFactory,
    IPromptTemplateProvider promptTemplates,
    ILogger<PlanConsolidator> logger)
{
    private const string AssessmentJsonExample =
        """{ "summary_items": [{ "order": 1, "content": "..." }], "assessments": [{ "file": "src/Foo.cs", "line": 42, "title": "...", "status": "confirmed", "reason": "..." }] }""";

    public async Task<ConsolidationResult> ConsolidateAsync(
        ConvergenceCheckContext context,
        List<DiscussionEntry> discussionLog,
        bool escalated,
        CancellationToken cancellationToken,
        ConsolidationRequest? request = null)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);

        var discussionText = string.Join("\n\n---\n\n", discussionLog.Select(e =>
            $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"));

        context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket);

        var systemPrompt = promptTemplates.Get("plan-consolidator-system");

        var inputSection = ticket is not null
            ? $"## Ticket\n{ticket.Title}\n{ticket.Description}"
            : "## Analysis Target\nSee discussion below for context.";

        var escalationNote = escalated
            ? "\n\n## Escalation\nNOTE: Not all roles agreed. Note the dissenting views in the summary."
            : "";

        var userPrompt = promptTemplates.Get("plan-consolidator-user")
            .Replace("{InputSection}", inputSection)
            .Replace("{DiscussionText}", discussionText)
            .Replace("{EscalationNote}", escalationNote)
            .Replace("{AssessmentJsonExample}", AssessmentJsonExample);

        var llmResponse = await llmClient.CompleteAsync(
            systemPrompt, userPrompt, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(llmResponse);

        var parseResult = ConsolidationResponseParser.Parse(llmResponse.Text, logger);

        var title = request?.Title ?? ticket?.Title ?? "Discussion Findings";
        var discussion = new ConsolidatedDiscussion(
            title, parseResult.Findings, parseResult.Assessments, parseResult.RawSummary);

        context.Pipeline.Set(ContextKeys.ConsolidatedDiscussion, discussion);
        context.Pipeline.Set(ContextKeys.ConsolidatedPlan, parseResult.RawSummary);

        if (parseResult.Assessments.Count > 0)
        {
            context.Pipeline.Set(ContextKeys.FindingAssessments, parseResult.Assessments);
            logger.LogInformation(
                "Parsed {Count} finding assessments ({Confirmed} confirmed, {FP} false_positive)",
                parseResult.Assessments.Count,
                parseResult.Assessments.Count(a => a.Status == "confirmed"),
                parseResult.Assessments.Count(a => a.Status == "false_positive"));
        }

        logger.LogInformation(
            "Consolidated discussion: {Title} ({Findings} findings, {Chars} chars)",
            title, parseResult.Findings.Count, parseResult.RawSummary.Length);

        return new ConsolidationResult(true, discussion);
    }
}

/// <summary>
/// Caller-provided context for pipeline-agnostic consolidation.
/// </summary>
public sealed record ConsolidationRequest(string Title);

/// <summary>
/// Result of a consolidation attempt.
/// </summary>
public sealed record ConsolidationResult(
    bool Success,
    ConsolidatedDiscussion? Discussion);
