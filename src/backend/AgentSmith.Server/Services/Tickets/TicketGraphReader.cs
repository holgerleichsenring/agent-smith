using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-14-dbcen: the requires graph of a project, or of every configured project — every ticket on either
/// end of a stored requires, an edge from each dependent to what it requires. A node's bucket is the list row's,
/// read through <see cref="TicketListReader.AllAsync"/>, so a node and its row cannot disagree; its state is
/// computed per read and never stored.
/// </summary>
public sealed class TicketGraphReader(
    TicketListReader list, ITicketRequirementStore requirements, ITicketLandingStore landings, TicketProjects projects)
{
    private sealed record End(string Tracker, string Key, string TicketId, string Project);

    public async Task<TicketGraphView> ReadAsync(ProjectScope project, CancellationToken ct)
    {
        var configured = projects.All();
        var names = project.Only ?? [.. configured.Keys];
        var edges = new List<TicketRequirementEdge>();
        foreach (var name in names) edges.AddRange(await requirements.ByProjectAsync(name, ct));
        if (edges.Count == 0) return new TicketGraphView([], []);
        var ends = edges.SelectMany(e => new[]
            {
                new End(e.Tracker, e.TicketKey, e.TicketId, e.Project),
                new End(e.Tracker, e.RequiredTicketKey, e.RequiredTicketId, e.Project),
            })
            .DistinctBy(e => (e.Tracker, e.Key)).ToList();
        var all = await list.AllAsync(project, ct);
        var summaries = TicketNodeStates.Summaries(all, p => configured.GetValueOrDefault(p));
        var landed = await TicketNodeStates.LandedAsync(landings, ends.Select(e => (e.Tracker, e.Key)), ct);
        var nodes = ends.Select(e => Node(e, summaries.GetValueOrDefault((e.Tracker, e.Key)),
            landed.GetValueOrDefault((e.Tracker, e.Key)), configured.GetValueOrDefault(e.Project))).ToList();
        return new TicketGraphView(nodes,
            [.. edges.Select(e => new TicketGraphEdgeView(Id(e.Tracker, e.TicketKey), Id(e.Tracker, e.RequiredTicketKey))).Distinct()]);
    }

    internal static string Id(string tracker, string key) => $"{tracker}:{key}";

    private static TicketGraphNodeView Node(End end, TicketSummaryView? summary, TicketLandingFact? landing, ResolvedProject? project)
    {
        var key = project is null ? end.TicketId : FiledTicketKey.Of(project.Tracker.Type, end.TicketId);
        return new TicketGraphNodeView(Id(end.Tracker, end.Key), summary?.Project ?? end.Project, end.TicketId, key,
            summary?.Title ?? key, TicketNodeStates.Of(landing, summary), summary?.Bucket ?? TicketBucket.Idle, summary is not null);
    }
}
