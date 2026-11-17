using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-17-5701a: a ticket's spec taken out of a draft, or all of them by deleting <c>tickets/&lt;key&gt;/</c>. A spec
/// another spec of the ticket requires stays until that one is rewritten, as an epic child does.
/// </summary>
public static class TicketSpecRemoval
{
    public static DraftWrite Remove(ProposalDraft draft, string key, string label)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Tickets.FirstOrDefault(t => t.Key == key) is not { } ticket || ticket.Specs.All(s => s.SpecId != label))
            return DraftWrite.Refused($"{DraftPath.Shown(DraftPath.OfTicketSpec(key, label))} is no file of this draft — "
                + $"it holds {DraftFiles.Listed(draft)}");
        var waiting = ticket.Specs.Where(s => s.Requires.Contains(label)).Select(s => s.SpecId).ToList();
        return waiting.Count > 0
            ? DraftWrite.Refused($"'{string.Join("', '", waiting)}' require '{label}' — edit or delete them first")
            : Replaced(draft, ticket with { Specs = [.. ticket.Specs.Where(s => s.SpecId != label)] });
    }

    /// <summary>Every spec of the ticket <paramref name="key"/> deleted; the ticket stays.</summary>
    public static DraftWrite Empty(ProposalDraft draft, string key)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return draft.Tickets.FirstOrDefault(t => t.Key == key) is { } ticket
            ? Replaced(draft, ticket with { Specs = [] })
            : DraftWrite.Refused($"{DraftPath.Shown(DraftPath.Tickets + key)}/ is no directory of this draft — it holds "
                + DraftFiles.Listed(draft));
    }

    private static DraftWrite Replaced(ProposalDraft draft, GraphTicket ticket) =>
        DraftWrite.To(draft with { Tickets = [.. draft.Tickets.Select(t => t.Key == ticket.Key ? ticket : t)] });
}
