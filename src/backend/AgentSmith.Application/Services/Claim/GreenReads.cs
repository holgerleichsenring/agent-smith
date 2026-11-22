using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-22-9719d: the reads <see cref="GreenPrerequisites"/> judges a tip by, for many dependents of one project:
/// each prerequisite's newest attempt end and close mark, and each ticket's requirement edges, read once and set-based.
/// The answers are the per-dependent ones — green when no unlanded prerequisite is closed and every one's newest attempt
/// <see cref="GreenPrerequisites.IsGreen"/>; the tip the one whose chain reaches every other.
/// </summary>
public sealed class GreenReads
{
    private readonly ITicketRequirementStore _requirements;
    private readonly IPreviousAttemptReader _attempts;
    private readonly ITicketClosedMarkStore _closed;
    private readonly string _project;
    private readonly Dictionary<string, PreviousAttempt?> _ends = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Tracker, string Key), bool> _closedKeys = [];
    private readonly Dictionary<(string Tracker, string Key), IReadOnlyList<TicketRequirementEdge>> _edges = [];

    internal GreenReads(ITicketRequirementStore requirements, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed,
        string project, IEnumerable<KeyValuePair<(string Tracker, string Key), IReadOnlyList<TicketRequirementEdge>>>? known)
    {
        (_requirements, _attempts, _closed, _project) = (requirements, attempts, closed, project);
        foreach (var (key, edges) in known ?? []) _edges[key] = edges;
    }

    /// <summary>Reads the end and close mark of every prerequisite the edges name that is not read yet: one close-mark
    /// read per tracker connection and one attempt read.</summary>
    public async Task PrefetchAsync(IEnumerable<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        var edges = unmet.ToList();
        foreach (var tracker in edges.Where(e => !_closedKeys.ContainsKey((e.Tracker, e.RequiredTicketKey))).GroupBy(e => e.Tracker))
        {
            var keys = tracker.Select(e => e.RequiredTicketKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var marked = (await _closed.OfAsync(tracker.Key, [.. keys], ct)).Select(m => m.TicketKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys) _closedKeys[(tracker.Key, key)] = marked.Contains(key);
        }
        var ids = edges.Select(e => e.RequiredTicketId).Where(id => !_ends.ContainsKey(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return;
        var ends = await _attempts.LatestEndsAsync(_project, ids, ct);
        foreach (var id in ids) _ends[id] = ends.GetValueOrDefault(id);
    }

    /// <summary>The edge whose prerequisite is the stack tip, or null; with <paramref name="requireGreen"/> every
    /// unlanded prerequisite must be green as well.</summary>
    public async Task<TicketRequirementEdge?> TipAsync(IReadOnlyList<TicketRequirementEdge> unmet, bool requireGreen, CancellationToken ct)
    {
        var distinct = unmet.DistinctBy(e => e.RequiredTicketKey, StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return null;
        if (requireGreen && !await AllGreenAsync(distinct, ct)) return null;
        foreach (var candidate in distinct)
            if (await ReachesAllAsync(candidate, distinct, ct)) return candidate;
        return null;
    }

    private async Task<bool> AllGreenAsync(IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        await PrefetchAsync(unmet, ct);
        return !unmet.Any(e => _closedKeys[(e.Tracker, e.RequiredTicketKey)])
            && unmet.All(e => GreenPrerequisites.IsGreen(_ends[e.RequiredTicketId]));
    }

    // Walks the candidate's requirement chain; the seen set bounds it on any graph.
    private async Task<bool> ReachesAllAsync(
        TicketRequirementEdge candidate, IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        var others = unmet.Select(e => e.RequiredTicketKey).Where(k => k != candidate.RequiredTicketKey)
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
        return others.Count == 0;
    }

    private async Task<IReadOnlyList<TicketRequirementEdge>> EdgesAsync(string tracker, string key, CancellationToken ct) =>
        _edges.TryGetValue((tracker, key), out var known) ? known
            : _edges[(tracker, key)] = await _requirements.RequiredOfAsync(tracker, key, ct);
}
