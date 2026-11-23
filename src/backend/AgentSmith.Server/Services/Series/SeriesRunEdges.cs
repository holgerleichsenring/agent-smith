using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcf: what each requires edge between two tickets of a series means for the branches, read from the ONE
/// per-repo stack a start is decided by (<see cref="GreenReads.StackPerRepoAsync"/>, 2026-11-23-31dca) — never derived a
/// second time. Per dependent: for one that never started, a prerequisite that is not green, or reaches an unlanded one
/// that is not green, is unknown (the start waits on it, as RequirementHoldRule does); one whose chain reaches a merge the
/// stack names needs that merge; any other is stacked. A started dependent is never held, so only its stack answers. A
/// project that starts a dependent only once its requirements landed meets every requirement by a merge. Reads go through
/// one read set for the series, prefetched set-based.
/// </summary>
public sealed class SeriesRunEdges(GreenPrerequisites green)
{
    public async Task<IReadOnlyList<SeriesRunEdgeView>> OfAsync(
        SeriesRunProjectFacts facts, IReadOnlyCollection<string> ticketIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var series = ticketIds.ToHashSet(StringComparer.Ordinal);
        var unmet = facts.Edges.Where(p => series.Contains(p.Key)).ToDictionary(p => p.Key, p => (IReadOnlyList<TicketRequirementEdge>)
            [.. p.Value.Where(e => !facts.Landed.Contains(e.RequiredTicketId)).DistinctBy(e => e.RequiredTicketKey, StringComparer.Ordinal)],
            StringComparer.Ordinal);
        var kinds = facts.Project.RequiresStart == RequiresStart.OnGreenPush ? await StackedKindsAsync(facts, unmet, ct) : [];
        var views = new List<SeriesRunEdgeView>();
        foreach (var (ticket, edges) in facts.Edges.Where(p => series.Contains(p.Key)))
            foreach (var edge in edges.Where(e => series.Contains(e.RequiredTicketId)).DistinctBy(e => e.RequiredTicketId, StringComparer.Ordinal))
                views.Add(View(edge, facts.Landed.Contains(edge.RequiredTicketId) ? SeriesRunEdgeKinds.Landed
                    : kinds.GetValueOrDefault((ticket, edge.RequiredTicketKey), SeriesRunEdgeKinds.Merge)));
        return views;
    }

    // Every dependent's unlanded edges by (dependent id, prerequisite key), through one read set and one started read.
    private async Task<Dictionary<(string, string), string>> StackedKindsAsync(
        SeriesRunProjectFacts facts, IReadOnlyDictionary<string, IReadOnlyList<TicketRequirementEdge>> unmet, CancellationToken ct)
    {
        var held = unmet.Where(p => p.Value.Count > 0).ToList();
        var started = held.Count == 0 ? new HashSet<string>() : await green.StartedAsync(facts.Project.Name, [.. held.Select(p => p.Key)], ct);
        var reads = green.Reads(facts.Project.Name,
            facts.Edges.Values.Where(e => e.Count > 0).Select(e => KeyValuePair.Create((e[0].Tracker, e[0].TicketKey), e)));
        await reads.PrefetchAsync(held.SelectMany(p => p.Value), ct);
        var kinds = new Dictionary<(string, string), string>();
        foreach (var (ticket, edges) in held)
            foreach (var (key, kind) in await KindsAsync(reads, edges, started.Contains(ticket), ct)) kinds[(ticket, key)] = kind;
        return kinds;
    }

    // One dependent's unlanded edges by prerequisite key, from its per-repo stack.
    private static async Task<Dictionary<string, string>> KindsAsync(
        GreenReads reads, IReadOnlyList<TicketRequirementEdge> unmet, bool wasStarted, CancellationToken ct)
    {
        var notGreen = wasStarted ? [] : (await reads.NotGreenAsync(await reads.UnlandedChainAsync(unmet, ct), ct))
            .Select(e => e.RequiredTicketKey).ToHashSet(StringComparer.Ordinal);
        var merges = (await reads.StackPerRepoAsync(unmet, ct)).Merges.Select(e => e.RequiredTicketKey).ToHashSet(StringComparer.Ordinal);
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var edge in unmet)
            kinds[edge.RequiredTicketKey] = await ReachesAsync(reads, edge, notGreen, ct) ? SeriesRunEdgeKinds.Unknown
                : await ReachesAsync(reads, edge, merges, ct) ? SeriesRunEdgeKinds.Merge : SeriesRunEdgeKinds.Stacked;
        return kinds;
    }

    // Whether the prerequisite, or an unlanded one its chain reaches, is among these keys.
    private static async Task<bool> ReachesAsync(
        GreenReads reads, TicketRequirementEdge edge, IReadOnlySet<string> keys, CancellationToken ct) =>
        keys.Count > 0 && (await reads.UnlandedChainAsync([edge], ct)).Any(e => keys.Contains(e.RequiredTicketKey));

    private static SeriesRunEdgeView View(TicketRequirementEdge edge, string kind) => new(edge.TicketId, edge.RequiredTicketId, kind);
}
