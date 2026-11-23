using AgentSmith.Application.Services.Holds;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dci: the admin holds of a scope, from the ticket list's one read (<see cref="TicketListReader.AllAsync"/>),
/// so System → Needs attention names the hold the ticket's row, its page and Your move name — a precondition stop and an
/// escalated wait alike, the longest held first.
/// </summary>
public sealed class AdminHoldsReader(TicketListReader list)
{
    public async Task<AdminHoldsView> ReadAsync(ProjectScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var all = await list.AllAsync(scope, ct);
        var holds = all.Runs.Select(t => t.Summary).Concat(all.NeverRun)
            .Where(s => s.Hold?.Addressee == TicketHoldAddressees.Admin)
            .Select(s => new AdminHoldView(s.Project, s.TicketId, s.TicketKey, s.Title, s.Hold!.Since ?? s.Since, s.Hold))
            .OrderBy(h => h.Since ?? DateTimeOffset.MaxValue).ThenBy(h => h.Project, StringComparer.Ordinal)
            .ThenBy(h => h.TicketId, StringComparer.Ordinal)
            .ToList();
        return new AdminHoldsView(holds);
    }
}
