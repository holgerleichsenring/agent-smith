using AgentSmith.Application.Services.Holds;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcg: the holds of every ticket of the scope, as the ticket list reads them (<see cref="TicketListReader.AllAsync"/>)
/// — so the series page, the ticket page and Your move name one hold for a ticket. Keyed by project, then ticket id.
/// </summary>
public sealed class SeriesRunHolds(TicketListReader list)
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, TicketHold>>> ReadAsync(ProjectScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var all = await list.AllAsync(scope, ct);
        return all.Runs.Select(r => r.Summary).Concat(all.NeverRun).Where(s => s.Hold is not null)
            .GroupBy(s => s.Project, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, TicketHold>)g
                .GroupBy(s => s.TicketId, StringComparer.Ordinal).ToDictionary(t => t.Key, t => t.First().Hold!, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }
}
