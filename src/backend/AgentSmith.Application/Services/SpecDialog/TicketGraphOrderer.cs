using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcej: puts a proposed list of tickets in the order its requires edges allow —
/// Kahn's algorithm over sibling keys, ties broken by the proposal's own order, so a list already
/// in order stays as proposed. A <c>ticket:&lt;id&gt;</c> requirement names a ticket filed before
/// this list and never constrains the order. A list whose edges leave a key waiting is refused,
/// naming every key still waiting.
/// </summary>
public sealed class TicketGraphOrderer
{
    public (IReadOnlyList<GraphTicket> Tickets, string? Error) Order(IReadOnlyList<GraphTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        var siblings = tickets.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        var pending = new List<GraphTicket>(tickets);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<GraphTicket>(tickets.Count);

        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(t => t.Requires.Where(siblings.Contains).All(placed.Contains));
            if (next is null)
                return (tickets, "the tickets' requires cannot be put in an order — "
                    + $"'{string.Join("', '", pending.Select(t => t.Key))}' each wait on another ticket of this list");
            ordered.Add(next);
            placed.Add(next.Key);
            pending.Remove(next);
        }
        return (ordered, null);
    }
}
