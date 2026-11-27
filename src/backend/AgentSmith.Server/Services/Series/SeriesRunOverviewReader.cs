using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcb: the series list — every series run of the scope's projects, newest activity first, each with its
/// progress and the ticket that merges next. Set-based: one store read, one name read, and per project the fixed reads of
/// <see cref="SeriesRunProjectReads"/>, never one per ticket. A series of a project no longer configured is left out.
/// 2026-11-23-31dcg: its held count is the tickets whose hold names someone and is not the next merge, from the holds the ticket list reads.
/// </summary>
public sealed class SeriesRunOverviewReader(
    ISeriesRunStore store, TicketProjects projects, SeriesStatusReader status, SeriesRunNames names)
{
    public async Task<IReadOnlyList<SeriesRunRow>> ReadAsync(ProjectScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var records = (await store.ListAsync(scope.Only, ct)).Where(r => scope.Admits(r.Project) && projects.Of(r.Project) is not null).ToList();
        if (records.Count == 0) return [];
        var called = await names.OfAsync(records, ct);
        var read = await status.StatusesAsync(records, withHolds: true, ct); // 2026-11-25-9992a: the one state
        return [.. records.Select(r => Row(r, called[r.Id], read[r.Id]))];
    }

    // 849e: read holds, none is no merge
    private static SeriesRunRow Row(SeriesRunRecord record, string name, SeriesRunStatus status) => new(
        record.Id, name, record.Project, record.State, status.Shape.Counts,
        status.Shape.NextMerge, SeriesRunQuestion.Of(record), record.LastActivityAt);
}
