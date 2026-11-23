using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-11-22-9719d: the reads <see cref="GreenPrerequisites"/> judges a tip by, for many dependents of one project:
/// each prerequisite's newest attempt end and close mark, and each ticket's requirement edges, read once and set-based.
/// The answers are the per-dependent ones — green when no unlanded prerequisite is closed and every one's newest attempt
/// <see cref="GreenPrerequisites.IsGreen"/>; the tip the one whose chain reaches every other.
/// <para>2026-11-23-97c2b: the edges are walked by <see cref="RequirementChain"/>; the repos each prerequisite has a pull
/// request in are read once as well, for the tip per repo. 2026-11-23-31dca: the ticket-level tip is gone — every stack is
/// per repo (<see cref="StackPerRepoAsync"/>).</para>
/// </summary>
public sealed class GreenReads
{
    private readonly RequirementChain _chain;
    private readonly IPreviousAttemptReader _attempts;
    private readonly ITicketClosedMarkStore _closed;
    private readonly string _project;
    private readonly Dictionary<string, PreviousAttempt?> _ends = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Tracker, string Key), bool> _closedKeys = [];
    private readonly Dictionary<string, IReadOnlySet<string>> _repos = new(StringComparer.OrdinalIgnoreCase); // 97c2b

    internal GreenReads(RequirementChain chain, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed, string project) =>
        (_chain, _attempts, _closed, _project) = (chain, attempts, closed, project);

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

    /// <summary>
    /// 2026-11-23-31dca: the stack per repo — for each repo an unlanded prerequisite of the chain has a pull request in, the
    /// one with work there whose chain reaches every other one with work there (2026-11-23-97c2b's tip per repo): its branch
    /// in that repo carries theirs. A repo where none does names the fewest merges that leave one (2026-11-22-6d85d); a repo
    /// no unlanded prerequisite worked in has no entry. Greenness is not asked here (<see cref="NotGreenAsync"/>).
    /// </summary>
    public async Task<RepoStack> StackPerRepoAsync(IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        var chain = await _chain.UnlandedAsync(unmet, ct);
        var repos = await ReposAsync(chain, ct);
        var bases = new Dictionary<string, string>(StringComparer.Ordinal);
        var merges = new List<TicketRequirementEdge>();
        foreach (var worked in chain.SelectMany(e => repos[e.RequiredTicketId].Select(r => (Repo: r, Edge: e)))
                     .GroupBy(p => p.Repo, StringComparer.Ordinal))
        {
            IReadOnlyList<TicketRequirementEdge> among = [.. worked.Select(p => p.Edge)];
            if (await _chain.TipAmongAsync(among, ct) is { } tip) bases[worked.Key] = tip.RequiredTicketId;
            else merges.AddRange(await _chain.FewestMergesAsync(among, ct));
        }
        return new RepoStack(bases, [.. merges.DistinctBy(e => e.RequiredTicketKey, StringComparer.Ordinal)]);
    }

    /// <summary>2026-11-23-97c2b: the unmet edges and every unlanded prerequisite their chains reach, each once, in walk order.</summary>
    public Task<IReadOnlyList<TicketRequirementEdge>> UnlandedChainAsync(IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct) =>
        _chain.UnlandedAsync(unmet, ct);

    /// <summary>2026-11-23-97c2b: the edges and every prerequisite their chains reach, landed or not, each once, in walk order.</summary>
    public Task<IReadOnlyList<TicketRequirementEdge>> ChainAsync(IReadOnlyList<TicketRequirementEdge> edges, CancellationToken ct) =>
        _chain.ReachedAsync(edges, ct);

    /// <summary>The unlanded prerequisites among these that are not green: a close mark, or a newest attempt that is not
    /// <see cref="GreenPrerequisites.IsGreen"/>; empty when every one is green.</summary>
    public async Task<IReadOnlyList<TicketRequirementEdge>> NotGreenAsync(IReadOnlyList<TicketRequirementEdge> unmet, CancellationToken ct)
    {
        await PrefetchAsync(unmet, ct);
        return [.. unmet.Where(e => _closedKeys[(e.Tracker, e.RequiredTicketKey)] || !GreenPrerequisites.IsGreen(_ends[e.RequiredTicketId]))];
    }

    // The repos each prerequisite has a pull request in, read once for the ones not read yet.
    private async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> ReposAsync(
        IReadOnlyList<TicketRequirementEdge> edges, CancellationToken ct)
    {
        var ids = edges.Select(e => e.RequiredTicketId).Where(id => !_repos.ContainsKey(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0) return _repos;
        var read = await _attempts.PullRequestReposAsync(_project, ids, ct);
        foreach (var id in ids) _repos[id] = read.GetValueOrDefault(id) ?? new HashSet<string>(StringComparer.Ordinal);
        return _repos;
    }
}
