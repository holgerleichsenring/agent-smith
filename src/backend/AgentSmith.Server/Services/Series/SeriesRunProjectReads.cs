using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcb: the facts of every series ticket of one project in a fixed number of reads, whatever the number of
/// series or tickets — the project's requires edges, one landing read over the tickets and what they require, the
/// project's pull-request records and the newest attempts. One after another: the reads share the request's unit of work.
/// </summary>
public sealed class SeriesRunProjectReads(
    ITicketRequirementStore requirements, ITicketLandingStore landings, PrReviewRecordRepository records,
    IPreviousAttemptReader attempts, SeriesRunLaneReads lanes)
{
    public async Task<SeriesRunProjectFacts> ReadAsync(ResolvedProject project, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var ids = tickets.Select(t => t.TicketId).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var edges = (await requirements.ByProjectAsync(project.Name, ct)).Where(e => ids.Contains(e.TicketId))
            .GroupBy(e => e.TicketId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TicketRequirementEdge>)[.. g], StringComparer.Ordinal);
        var requires = edges.ToDictionary(p => p.Key,
            p => (IReadOnlyList<string>)[.. p.Value.Select(e => e.RequiredTicketId).Distinct(StringComparer.Ordinal)], StringComparer.Ordinal);
        var asked = ids.Concat(requires.Values.SelectMany(r => r)).Distinct(StringComparer.Ordinal).Select(id => Key(project, id)).ToList();
        var found = await landings.LandedAsync(project.Tracker.Name, asked, ct);
        var landed = found.Select(l => l.TicketId).ToHashSet(StringComparer.Ordinal);
        var prs = (await records.ByTicketsAsync(project.Name, ids, ct)).ToLookup(r => r.TicketId, StringComparer.Ordinal);
        var newest = await attempts.LatestEndsAsync(project.Name, ids, ct);
        // 2026-11-27-ecf2b: which pull requests carry only the spec record, and where each newest attempt's specs sit.
        var recordOnly = await lanes.RecordOnlyAsync([.. prs.SelectMany(g => g).Select(r => r.RunId).Distinct(StringComparer.Ordinal)], ct);
        // A ticket's runs: its newest attempt and the runs its pull requests name; its specs are the newest of them with a cut.
        var runsOf = ids.ToDictionary(id => id, id => (IReadOnlyList<string>)[.. prs[id].Select(r => r.RunId)
            .Append(newest.GetValueOrDefault(id)?.RunId ?? string.Empty).Where(r => r.Length > 0).Distinct(StringComparer.Ordinal)
            .OrderByDescending(r => r, StringComparer.Ordinal)], StringComparer.Ordinal);
        var specRepos = await lanes.SpecReposAsync([.. runsOf.Values.SelectMany(r => r).Distinct(StringComparer.Ordinal)], ct);
        return new SeriesRunProjectFacts(project, requires, landed, prs, newest)
        {
            Edges = edges, // 2026-11-25-9992a: and when each landed
            LandedAt = found.GroupBy(l => l.TicketId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Min(l => l.LandedAt), StringComparer.Ordinal),
            RecordOnly = recordOnly,
            SpecRepos = runsOf.Select(t => (t.Key, Run: t.Value.FirstOrDefault(specRepos.ContainsKey))).Where(t => t.Run is not null)
                .ToDictionary(t => t.Key, t => specRepos[t.Run!], StringComparer.Ordinal),
        };
    }

    private static string Key(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
}
