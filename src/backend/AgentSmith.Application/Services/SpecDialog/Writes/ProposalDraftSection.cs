using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the draft the turn starts from — the proposal awaiting a decision, item by item with the handle
/// each is rewritten or removed by. Empty when the turn starts with no draft.
/// </summary>
public static class ProposalDraftSection
{
    public static string Render(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot)
            || slot is null || slot.Proposal.Current.IsEmpty) return string.Empty;
        var draft = slot.Proposal.Current;
        var sb = new StringBuilder().AppendLine().AppendLine("## Your draft")
            .AppendLine("What your write tools hold now. Rewrite an item by its key or id, remove it with remove_item; "
                + "a turn that writes nothing proposes nothing new and this draft stands as it is.");
        foreach (var spec in draft.Specs)
            sb.AppendLine($"### spec {spec.Draft.SpecId}{(spec.Parent ? " (parent)" : string.Empty)}")
                .AppendLine("```yaml").AppendLine(spec.Draft.Yaml.TrimEnd()).AppendLine("```");
        foreach (var ticket in draft.Tickets) Ticket(sb, ticket);
        if (draft.Bug is { } bug) sb.AppendLine($"### bug: {bug.Title}").AppendLine(bug.Description);
        if (draft.Comment is { } comment) sb.AppendLine("### comment").AppendLine(comment);
        return sb.ToString().TrimEnd();
    }

    private static void Ticket(StringBuilder sb, GraphTicket ticket)
    {
        sb.AppendLine($"### ticket {ticket.Key}: {ticket.Title}").AppendLine(ticket.Requirement);
        foreach (var done in ticket.Done) sb.AppendLine($"- done: {done}");
        if (ticket.Requires.Count > 0) sb.AppendLine($"- requires: {string.Join(", ", ticket.Requires)}");
        if (ticket.Repos.Count > 0) sb.AppendLine($"- repos: {string.Join(", ", ticket.Repos)}");
    }
}
