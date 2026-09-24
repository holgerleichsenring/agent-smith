using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// 2026-09-24-b3c1: the classifier's user prompt — the per-repo context inventory, the
/// delimited (untrusted) ticket and the conversation. Lifted out of RepoScopeClassifier,
/// which makes the call and has no business also being a prompt renderer. Sibling of
/// MasterUserPrompt and ExpectationPromptComposer, which render the same ticket block.
/// </summary>
internal static class RepoScopePrompt
{
    internal static string Build(
        Ticket ticket, IReadOnlyList<TicketComment>? comments,
        IReadOnlyList<RepoConnection> repos,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        var sb = new StringBuilder("## Repositories in this project\n");
        foreach (var repo in repos)
            sb.AppendLine(DescribeRepo(repo, inventory));
        sb.AppendLine();
        // 2026-09-24-b3c1: an Azure DevOps body arrives as HTML — read as TEXT here,
        // through the converter the derivation path has used since p0399.
        var criteria = TicketHtmlConverter.ToText(ticket.AcceptanceCriteria);
        // p0316: ticket fields are untrusted — delimited so an embedded injection
        // reads as data, exactly like the master prompts treat them.
        sb.AppendLine(TicketPromptDelimiters.Wrap($"""
            **Title:** {ticket.Title}
            **Description:** {TicketHtmlConverter.ToText(ticket.Description)}
            **Acceptance Criteria:** {(criteria.Length > 0 ? criteria : "None specified")}
            """));
        var conversation = TicketConversationPromptSection.Render(comments);
        if (conversation.Length > 0) sb.AppendLine().AppendLine(conversation);
        return sb.ToString();
    }

    private static string DescribeRepo(
        RepoConnection repo, IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        var name = repo.Name ?? string.Empty;
        if (!inventory.TryGetValue(name, out var contexts) || contexts.Count == 0)
            return $"- {name}";
        var described = contexts.Select(c =>
        {
            var purpose = string.IsNullOrWhiteSpace(c.Purpose) ? string.Empty : $" — {c.Purpose}";
            return $"'{c.ContextName}' (workdir={c.Workdir}, lang={c.Language ?? "unknown"}){purpose}";
        });
        return $"- {name}: contexts {string.Join("; ", described)}";
    }
}
