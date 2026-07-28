using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: composes the drafting user prompt — the delimited (untrusted) ticket
/// plus the AnalyzeCode-derived per-repo code maps and the ticket conversation,
/// so the draft is grounded in analysis, not just the raw ticket text. p0384:
/// the scoped repo list and one analysis block per repo enter the prompt so
/// acceptance criteria are drafted per repo, never against an unnamed single
/// codebase. Pure mapping.
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
        AppendScopedRepos(sb, pipeline);
        AppendCodeMaps(sb, pipeline);
        return sb.ToString();
    }

    private static void AppendConversation(StringBuilder sb, PipelineContext pipeline)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var conversation = TicketConversationPromptSection.Render(comments);
        if (conversation.Length > 0) sb.AppendLine().AppendLine(conversation);
    }

    // p0384: the scoped repo set — criteria must state, per repo, what change
    // (or explicit non-involvement) delivers the ticket.
    private static void AppendScopedRepos(StringBuilder sb, PipelineContext pipeline)
    {
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            ? r : null;
        if (repos is null || repos.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("## Repositories in scope");
        foreach (var repo in repos)
            sb.AppendLine($"- {repo.Name}");
        if (repos.Count > 1)
            sb.AppendLine(
                "Draft the criteria per repository: every repository above is either "
                + "covered by a criterion or explicitly not affected.");
    }

    private static void AppendCodeMaps(StringBuilder sb, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.RepoCodeMaps, out var maps) || maps is null || maps.Count == 0)
            return;
        sb.AppendLine();
        sb.AppendLine("## Codebase analysis");
        foreach (var (repoName, codeMap) in maps)
        {
            if (string.IsNullOrWhiteSpace(codeMap)) continue;
            sb.AppendLine($"### Repository: {repoName}");
            sb.AppendLine(codeMap);
        }
    }
}
