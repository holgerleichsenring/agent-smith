using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcj: the changes to a series' spec sets — every revision after the first of each member ticket's set, read
/// from the ticket branch the ticket page reads (<see cref="TicketBranchSpecs"/>), the tickets side by side. Whoever wrote
/// the revision — the attempt that applies an edit_series change or an approved proposal, an in-run revision — it shows;
/// nothing is stored for it. A change recorded but not yet applied by an attempt has written no revision, and an amendment
/// re-filed before any attempt starts the branch's history again at revision 1, so neither shows. Null when there is no such
/// series or its project is no longer configured.
/// </summary>
public sealed class SeriesRunChangesReader(ISeriesRunStore store, TicketProjects projects, TicketBranchSpecs branches)
{
    public async Task<SeriesRunChangesView?> ReadAsync(string id, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } record || projects.Of(record.Project) is null) return null;
        var ids = record.Tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal).ToList();
        var reads = await Task.WhenAll(ids.Select(ticketId => branches.ReadAsync(record.Project, ticketId, ct)));
        var read = ids.Zip(reads).ToList();
        var changes = read.Where(r => r.Second.Set is not null)
            .SelectMany(r => r.Second.Set!.Revisions.OrderBy(v => v.Number).Skip(1)
                .Select(v => new SeriesRunChangeView(r.First, v.Number, v.Cause, v.At)))
            .OrderByDescending(c => c.At).ThenBy(c => c.TicketId, StringComparer.Ordinal).ThenByDescending(c => c.Revision);
        var unread = read.Where(r => r.Second.Why is not null).Select(r => new SeriesRunUnreadBranchView(r.First, r.Second.Why!));
        return new SeriesRunChangesView([.. changes], [.. unread]);
    }
}
