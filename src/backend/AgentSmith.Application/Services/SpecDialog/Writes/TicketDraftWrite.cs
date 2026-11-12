using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: one ticket written into a draft — added, or replacing the ticket of the same key. The ticket is
/// checked as the list checks it (key, fields, requires naming a ticket of the draft or ticket:&lt;id&gt;, repositories
/// in scope), and the draft with it must still be orderable; a refusal names the ticket and the rule.
/// </summary>
public sealed class TicketDraftWrite(TicketGraphChecker checker, TicketGraphOrderer orderer)
{
    public DraftWrite Write(ProposalDraft draft, GraphTicket ticket, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(ticket);
        ticket = GraphTicketLines.Normalized(ticket);
        if (DraftKind.Conflict(draft, DraftKind.Tickets) is { } conflict) return DraftWrite.Refused(conflict);
        var siblings = draft.Tickets.Select(t => t.Key).Where(k => k != ticket.Key).ToHashSet(StringComparer.Ordinal);
        if (checker.Ticket(ticket, siblings, scope) is { } error) return DraftWrite.Refused(error);
        var at = draft.Tickets.ToList().FindIndex(t => t.Key == ticket.Key);
        List<GraphTicket> tickets = [.. draft.Tickets];
        if (at >= 0) tickets[at] = ticket;
        else tickets.Add(ticket);
        return orderer.Order(tickets).Error is { } cycle
            ? DraftWrite.Refused(cycle)
            : DraftWrite.To(draft with { Tickets = tickets });
    }
}
