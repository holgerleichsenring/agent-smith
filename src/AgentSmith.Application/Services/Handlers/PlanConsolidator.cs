using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Consolidates a multi-role discussion into structured findings via LLM.
/// Pipeline-agnostic: title and role come from the caller via ConsolidationRequest.
/// </summary>
public sealed class PlanConsolidator(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
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
        var discussionText = string.Join("\n\n---\n\n", discussionLog.Select(e =>
            $"{e.Emoji} {e.DisplayName} (Round {e.Round}):\n{e.Content}"));

        context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket);

        var systemPrompt = prompts.Get("plan-consolidator-system");

        var inputSection = ticket is not null
            ? $"## Ticket\n{ticket.Title}\n{ticket.Description}"
            : "## Analysis Target\nSee discussion below for context.";

        var escalationNote = escalated
            ? "\n\n## Escalation\nNOTE: Not all roles agreed. Note the dissenting views in the summary."
            : "";

        var userPrompt = prompts.Render("plan-consolidator-user", new Dictionary<string, string>
        {
            ["InputSection"] = inputSection,
            ["DiscussionText"] = discussionText,
            ["EscalationNote"] = escalationNote,
            ["AssessmentJsonExample"] = AssessmentJsonExample,
        });

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Planning);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);
        var responseText = response.Text ?? string.Empty;

        var parseResult = ConsolidationResponseParser.Parse(responseText, logger);

        var title = request?.Title ?? ticket?.Title ?? "Discussion Findings";
        var discussion = new ConsolidatedDiscussion(
            title, parseResult.Findings, parseResult.RawSummary);

        context.Pipeline.Set(ContextKeys.ConsolidatedDiscussion, discussion);
        context.Pipeline.Set(ContextKeys.ConsolidatedPlan, parseResult.RawSummary);

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
