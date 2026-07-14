using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: composes the drafting user prompt — the delimited (untrusted) ticket
/// plus the AnalyzeCode-derived code map and the ticket conversation, so the
/// draft is grounded in analysis, not just the raw ticket text. Pure mapping.
/// </summary>
internal static class ExpectationPromptComposer
{
    public static string ComposeUserPrompt(Ticket ticket, PipelineContext pipeline)
    {
        var sb = new StringBuilder();
        // p0316: ticket fields are untrusted — delimited so an embedded
        // injection reads as data, exactly like the master prompts treat them.
        sb.AppendLine(TicketPromptDelimiters.Wrap($"""
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
            """));
        AppendConversation(sb, pipeline);
        AppendCodeMap(sb, pipeline);
        return sb.ToString();
    }

    private static void AppendConversation(StringBuilder sb, PipelineContext pipeline)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var conversation = TicketConversationPromptSection.Render(comments);
        if (conversation.Length > 0) sb.AppendLine().AppendLine(conversation);
    }

    private static void AppendCodeMap(StringBuilder sb, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap)
            || string.IsNullOrWhiteSpace(codeMap))
            return;
        sb.AppendLine();
        sb.AppendLine("## Codebase analysis");
        sb.AppendLine(codeMap);
    }
}
