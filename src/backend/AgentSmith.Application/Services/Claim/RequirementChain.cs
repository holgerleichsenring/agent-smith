using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-23-97c2b: the requirement edges between prerequisites, each read once — the walks <see cref="GreenReads"/> asks:
/// which unlanded prerequisites a candidate's chain does not reach (moved here from GreenReads), every prerequisite a
/// ticket's chain reaches, and the unlanded ones among them (2026-11-22-6d85d's base walk, which UnmetTicketRequirements did
/// with its own reads).
/// </summary>
internal sealed class RequirementChain
{
    private readonly ITicketRequirementStore _requirements;
    private readonly ITicketLandingStore _landings;
    private readonly Dictionary<(string Tracker, string Key), IReadOnlyList<TicketRequirementEdge>> _edges = [];

    public RequirementChain(ITicketRequirementStore requirements, ITicketLandingStore landings,
        IEnumerable<KeyValuePair<(string Tracker, string Key), IReadOnlyList<TicketRequirementEdge>>>? known)
    {
        (_requirements, _landings) = (requirements, landings);
        foreach (var (key, edges) in known ?? []) _edges[key] = edges;
    }

    /// <summary>The prerequisites among <paramref name="among"/> the candidate's requirement chain does not reach; the seen set
    /// bounds the walk on any graph.</summary>
    public async Task<IReadOnlyList<TicketRequirementEdge>> UncoveredAsync(
        TicketRequirementEdge candidate, IReadOnlyList<TicketRequirementEdge> among, CancellationToken ct)
    {
        var others = among.Select(e => e.RequiredTicketKey).Where(k => k != candidate.RequiredTicketKey)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { candidate.RequiredTicketKey };
        var frontier = new Queue<string>([candidate.RequiredTicketKey]);
        while (others.Count > 0 && frontier.TryDequeue(out var key))
            foreach (var edge in await EdgesAsync(candidate.Tracker, key, ct))
            {
                if (!seen.Add(edge.RequiredTicketKey)) continue;
                others.Remove(edge.RequiredTicketKey);
                frontier.Enqueue(edge.RequiredTicketKey);
            }
        return [.. among.Where(e => others.Contains(e.RequiredTicketKey))];
    }

    /// <summary>The given edges, each prerequisite once, then every prerequisite their chains reach, landed or not, in walk
    /// order — one edge read per level of the walk; the seen set bounds it on any graph.</summary>
    public async Task<IReadOnlyList<TicketRequirementEdge>> ReachedAsync(IReadOnlyList<TicketRequirementEdge> from, CancellationToken ct)
    {
        var chain = from.DistinctBy(e => e.RequiredTicketKey, StringComparer.Ordinal).ToList();
        var seen = chain.Select(e => e.RequiredTicketKey).ToHashSet(StringComparer.Ordinal);
        for (var level = chain.ToList(); level.Count > 0;)
        {
            await ReadLevelAsync(level, ct);
            level = [.. level.SelectMany(at => _edges[(at.Tracker, at.RequiredTicketKey)]).Where(e => seen.Add(e.RequiredTicketKey))];
            chain.AddRange(level);
        }
        return chain;
    }

    /// <summary>The unmet edges, each prerequisite once, then every prerequisite their chains reach that has not landed, in
    /// walk order — one landing read for the deeper ones.</summary>
    public async Task<IReadOnlyList<TicketRequirementEdge>> UnlandedAsync(IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        var chain = await ReachedAsync(unmet, ct);
        var direct = unmet.Select(e => e.RequiredTicketKey).ToHashSet(StringComparer.Ordinal);
        var deeper = chain.Where(e => !direct.Contains(e.RequiredTicketKey)).ToList();
        if (deeper.Count == 0) return chain;
        var landed = (await _landings.LandedAsync(chain[0].Tracker, [.. deeper.Select(e => e.RequiredTicketKey)], ct))
            .Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        return [.. chain.Where(e => direct.Contains(e.RequiredTicketKey) || !landed.Contains(e.RequiredTicketKey))];
    }

    // The edges of every prerequisite of the level not read yet, one read per tracker connection.
    private async Task ReadLevelAsync(IReadOnlyList<TicketRequirementEdge> level, CancellationToken ct)
    {
        foreach (var tracker in level.Where(e => !_edges.ContainsKey((e.Tracker, e.RequiredTicketKey))).GroupBy(e => e.Tracker))
        {
            var read = await _requirements.RequiredOfManyAsync(tracker.Key, [.. tracker.Select(e => e.RequiredTicketKey).Distinct(StringComparer.Ordinal)], ct);
            foreach (var edge in tracker) _edges[(tracker.Key, edge.RequiredTicketKey)] = read.GetValueOrDefault(edge.RequiredTicketKey) ?? [];
        }
    }

    private async Task<IReadOnlyList<TicketRequirementEdge>> EdgesAsync(string tracker, string key, CancellationToken ct) =>
        _edges.TryGetValue((tracker, key), out var known) ? known
            : _edges[(tracker, key)] = await _requirements.RequiredOfAsync(tracker, key, ct);
}
