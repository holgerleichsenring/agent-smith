using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-25-9992a: the ONE place a series' ticket state is computed. Per project, in a fixed number of reads: the project's
/// series reads (<see cref="SeriesRunProjectReads"/>), the holds the ticket list reads (<see cref="SeriesRunHolds"/>, once per
/// project for every series asked), what each ticket's runs cost and when the first started; then per series the page's
/// shape (<see cref="SeriesRunTickets"/>) and each ticket's <see cref="SeriesTicketFact"/> from it, a parked question read
/// only for a parked attempt. The master's beat, its pause and the sweep, the dialog panel, the series page and its list all
/// read this. The hold inputs never do — they read the project reads and the merge order directly, so nothing is circular.
/// A series of a project no longer configured reads its tickets bare.
/// </summary>
public sealed class SeriesStatusReader(
    TicketProjects projects, SeriesRunProjectReads reads, SeriesRunTickets tickets, SeriesRunHolds holds, SeriesRunCosts costs,
    SeriesParkedQuestion parked) : ISeriesTicketFacts
{
    private static readonly IReadOnlyDictionary<string, TicketHold> NoHolds = new Dictionary<string, TicketHold>(StringComparer.Ordinal);

    public async Task<IReadOnlyList<SeriesTicketFact>> ReadAsync(SeriesRunRecord record, CancellationToken cancellationToken) =>
        (await ReadManyAsync([record], cancellationToken))[record.Id];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SeriesTicketFact>>> ReadManyAsync(
        IReadOnlyList<SeriesRunRecord> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        var read = await StatusesAsync(records, withHolds: true, cancellationToken);
        return records.ToDictionary(r => r.Id, r => read.TryGetValue(r.Id, out var status) ? status.Facts
            : (IReadOnlyList<SeriesTicketFact>)[.. r.Tickets.Select(t => new SeriesTicketFact(t))], StringComparer.Ordinal);
    }

    /// <param name="withHolds">False for a reader that needs the merge order only (the merge act), which then pays no
    /// ticket-list read; its kinds then fall back to the attempts.</param>
    public async Task<IReadOnlyDictionary<string, SeriesRunStatus>> StatusesAsync(
        IReadOnlyList<SeriesRunRecord> records, bool withHolds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);
        var known = records.Where(r => projects.Of(r.Project) is not null).ToList();
        var held = withHolds && known.Count > 0
            ? await holds.ReadAsync(ProjectScope.Of(known.Select(r => r.Project).Distinct(StringComparer.Ordinal)), ct) : null;
        var result = new Dictionary<string, SeriesRunStatus>(StringComparer.Ordinal);
        foreach (var project in known.GroupBy(r => r.Project, StringComparer.Ordinal))
        {
            var all = project.SelectMany(r => r.Tickets).ToList();
            var facts = await reads.ReadAsync(projects.Of(project.Key)!, all, ct);
            var spent = await costs.ByTicketAsync(project.Key, [.. all.Select(t => t.TicketId).Distinct(StringComparer.Ordinal)], ct);
            var projectHolds = held is null ? null : held.GetValueOrDefault(project.Key) ?? NoHolds;
            foreach (var record in project)
                result[record.Id] = await StatusAsync(record, facts, spent, projectHolds, ct);
        }
        return result;
    }

    private async Task<SeriesRunStatus> StatusAsync(SeriesRunRecord record, SeriesRunProjectFacts facts,
        IReadOnlyDictionary<string, SeriesTicketSpend> spent, IReadOnlyDictionary<string, TicketHold>? held, CancellationToken ct)
    {
        var shape = tickets.Of(record, facts, held);
        var views = shape.Tickets.GroupBy(v => v.TicketId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var needed = shape.MergeOrder.ToDictionary(s => s.TicketId, s => s.NeededBy ?? [], StringComparer.Ordinal);
        var list = new List<SeriesTicketFact>();
        foreach (var ticket in record.Tickets)
            list.Add(await FactAsync(ticket, views[ticket.TicketId], facts, spent, needed, ct));
        var byId = list.GroupBy(f => f.Ticket.TicketId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        shape = shape with
        {
            Tickets = [.. shape.Tickets.Select(v => Shown(v, byId[v.TicketId]))],
            Counts = SeriesRunCounts.Of(list),
        };
        return new SeriesRunStatus(record, facts, list, shape, held);
    }

    private static SeriesRunTicketView Shown(SeriesRunTicketView view, SeriesTicketFact fact) =>
        view with { Kind = fact.Kind, State = fact.State, Ci = fact.Ci, Working = fact.Working };

    private async Task<SeriesTicketFact> FactAsync(SeriesTicket ticket, SeriesRunTicketView view, SeriesRunProjectFacts facts,
        IReadOnlyDictionary<string, SeriesTicketSpend> spent, IReadOnlyDictionary<string, IReadOnlyList<string>> needed, CancellationToken ct)
    {
        var cost = spent.GetValueOrDefault(ticket.TicketId);
        var fact = new SeriesTicketFact(ticket)
        {
            Requires = facts.Requires.GetValueOrDefault(ticket.TicketId) ?? [],
            LandedAt = facts.LandedAt.TryGetValue(ticket.TicketId, out var at) ? at : null,
            FirstStartedAt = cost?.FirstStartedAt, CostUsd = cost?.CostUsd ?? 0,
            PullRequests = view.PullRequests, Hold = view.Hold, NeededBy = needed.GetValueOrDefault(ticket.TicketId) ?? [],
        };
        if (facts.Attempts.GetValueOrDefault(ticket.TicketId) is not { } latest) return fact;
        fact = fact with
        {
            LatestRunId = latest.RunId, LatestStatus = latest.Status, LatestFinished = latest.Finished,
            OpenedPullRequest = latest.OpenedPullRequest,
            // what the stop recorded; else the cancel reason or the end kind
            EndDetail = latest.Status == RunStatuses.Success ? null : latest.EndDetail?.Subject ?? latest.CancelReason ?? latest.EndKind,
        };
        return await parked.WithAsync(fact, latest, ct);
    }
}
