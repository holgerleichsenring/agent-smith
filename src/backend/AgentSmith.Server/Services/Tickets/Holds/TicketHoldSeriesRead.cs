using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Services.Series;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: the series half of a read's hold inputs — the newest series run each ticket is in, its step in that
/// series' merge order and whether the series is still active, from one series-run read for the scope and per project the
/// series page's own facts and order. 2026-11-23-849e: and per series its whole merge order and the never-run tickets that
/// start only once others merge, by the rule a start reads (one set-based read per project), moved out of
/// <see cref="TicketHoldInputsReader"/> under the file-length rule.
/// </summary>
internal static class TicketHoldSeriesRead
{
    internal sealed record Read(
        Dictionary<(string, string), string> Series, Dictionary<(string, string), SeriesRunMergeStep> Steps,
        HashSet<(string, string)> Active, Dictionary<string, TicketHoldSeriesPlan> Plans);

    // The newest series run that lists a ticket decides its series and its step; older ones it was in say nothing.
    internal static async Task<Read> ReadAsync(
        IServiceProvider services, ProjectScope scope, IReadOnlyDictionary<string, ResolvedProject> configured, CancellationToken ct)
    {
        var read = new Read([], [], [], new Dictionary<string, TicketHoldSeriesPlan>(StringComparer.Ordinal));
        var records = (await services.GetRequiredService<ISeriesRunStore>().ListAsync(scope.Only, ct))
            .Where(r => scope.Admits(r.Project) && configured.ContainsKey(r.Project)).ToList();
        if (records.Count == 0) return read;
        var reads = services.GetRequiredService<SeriesRunProjectReads>();
        var tickets = services.GetRequiredService<SeriesRunTickets>();
        foreach (var project in records.GroupBy(r => r.Project, StringComparer.Ordinal))
        {
            var resolved = configured[project.Key];
            var facts = await reads.ReadAsync(resolved, [.. project.SelectMany(r => r.Tickets)], ct);
            var waiting = await StartsOnMergeAsync(services, resolved, facts, [.. project.SelectMany(r => r.Tickets)], ct);
            foreach (var record in project.OrderBy(r => r.StartedAt)) Add(read, project.Key, record, tickets.Of(record, facts).MergeOrder, waiting);
        }
        return read;
    }

    private static void Add(Read read, string project, SeriesRunRecord record, IReadOnlyList<SeriesRunMergeStep> mergeOrder,
        IReadOnlySet<string> waiting)
    {
        var order = mergeOrder.ToDictionary(s => s.TicketId, StringComparer.Ordinal);
        read.Plans[record.Id] = new TicketHoldSeriesPlan(mergeOrder,
            [.. record.Tickets.Where(t => waiting.Contains(t.TicketId)).Select(t => t.Key).Distinct(StringComparer.Ordinal)]);
        foreach (var ticket in record.Tickets)
        {
            var key = (project, ticket.TicketId);
            read.Series[key] = record.Id;
            if (record.IsActive) read.Active.Add(key); else read.Active.Remove(key);
            if (order.TryGetValue(ticket.TicketId, out var step)) read.Steps[key] = step;
            else read.Steps.Remove(key);
        }
    }

    // The never-run, unlanded series tickets a start would hold until others merge (2026-11-22-6d85d), by ticket id.
    private static async Task<IReadOnlySet<string>> StartsOnMergeAsync(IServiceProvider services, ResolvedProject project,
        SeriesRunProjectFacts facts, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct)
    {
        var type = project.Tracker.Type.ToString().ToLowerInvariant();
        var keys = tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal)
            .Where(id => !facts.Attempts.ContainsKey(id) && !facts.Landed.Contains(id))
            .ToDictionary(id => TicketKey.For(type, id).Value, id => id, StringComparer.Ordinal);
        if (keys.Count == 0 || services.GetService<UnmetTicketRequirements>() is not { } rule) return new HashSet<string>();
        var holds = await rule.ForStartManyAsync(project, [.. keys.Keys], ct);
        return holds.Where(h => h.Value.MergesToStart.Count > 0 && keys.ContainsKey(h.Key))
            .Select(h => keys[h.Key]).ToHashSet(StringComparer.Ordinal);
    }
}
