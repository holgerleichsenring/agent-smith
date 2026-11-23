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
    ISeriesRunStore store, TicketProjects projects, SeriesRunProjectReads reads, SeriesRunTickets tickets, SeriesRunNames names,
    SeriesRunHolds holds)
{
    public async Task<IReadOnlyList<SeriesRunRow>> ReadAsync(ProjectScope scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var records = (await store.ListAsync(scope.Only, ct)).Where(r => scope.Admits(r.Project) && projects.Of(r.Project) is not null).ToList();
        if (records.Count == 0) return [];
        var called = await names.OfAsync(records, ct);
        var held = await holds.ReadAsync(ProjectScope.Of(records.Select(r => r.Project).Distinct(StringComparer.Ordinal)), ct);
        var rows = new Dictionary<string, SeriesRunRow>(StringComparer.Ordinal);
        foreach (var project in records.GroupBy(r => r.Project, StringComparer.Ordinal))
        {
            var facts = await reads.ReadAsync(projects.Of(project.Key)!, [.. project.SelectMany(r => r.Tickets)], ct);
            var projectHolds = held.GetValueOrDefault(project.Key) ?? new Dictionary<string, AgentSmith.Application.Services.Holds.TicketHold>();
            foreach (var record in project) // 849e: read holds, none is no merge; 8d16: and the question by them
                rows[record.Id] = Row(record, called[record.Id], tickets.Of(record, facts, projectHolds), projectHolds);
        }
        return [.. records.Select(r => rows[r.Id])];
    }

    private static SeriesRunRow Row(SeriesRunRecord record, string name, SeriesRunTicketsShape shape,
        IReadOnlyDictionary<string, AgentSmith.Application.Services.Holds.TicketHold> holds) => new(
        record.Id, name, record.Project, record.State, shape.Counts,
        shape.NextMerge, SeriesRunQuestion.Of(record, holds), record.LastActivityAt); // 2026-11-23-849e: the hold's rule
}
