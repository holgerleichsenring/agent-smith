using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-9dc0a: the server's check of a conflict pass — whether the merge is whole, whatever the master said. It
/// stages what the master resolved: the paths its file tools wrote that carry no conflict marker, and a path git still
/// lists unmerged that is gone from disk (a removal). A path still unmerged that the master neither wrote nor staged stays
/// unmerged — a marker file keeps its index stages for --ours/--theirs, and a conflict git left without markers (binary,
/// modify/delete, a merge driver) is never taken silently. Open is every path still unmerged, and every path carrying a
/// marker among the conflicted ones, the written ones and those whose staged content differs from both parents.
/// </summary>
public sealed class BaseMergeWholeness(BaseRefreshGit git)
{
    public async Task<BaseMergeOpen> CheckAsync(ISandbox sandbox, IReadOnlyList<string> conflicted, IReadOnlyList<string> wrote,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conflicted);
        ArgumentNullException.ThrowIfNull(wrote);
        var unmerged = await Unmerged(sandbox, ct);
        var marked = unmerged is null ? null : await git.MarkedAsync(sandbox, Union(conflicted, wrote, unmerged), ct);
        var missing = unmerged is null ? null : await git.MissingAsync(sandbox, unmerged, ct);
        if (unmerged is null || marked is null || missing is null) return BaseMergeOpen.All(conflicted, BaseMergeOpenPath.Unchecked);
        IReadOnlyList<string> stage = [.. wrote.Where(p => !marked.ContainsKey(p)).Concat(missing).Distinct(StringComparer.Ordinal)];
        if (!await git.StageAsync(sandbox, stage, ct)) return BaseMergeOpen.All(conflicted, BaseMergeOpenPath.Unchecked);
        var left = await Unmerged(sandbox, ct);
        var produced = await ProducedAsync(sandbox, ct);
        var markedNow = left is null || produced is null ? null : await git.MarkedAsync(sandbox, Union(conflicted, wrote, produced), ct);
        if (left is null || markedNow is null) return BaseMergeOpen.All(conflicted, BaseMergeOpenPath.Unchecked);
        return Open(left, markedNow);
    }

    private static BaseMergeOpen Open(IReadOnlyList<string> unmerged, IReadOnlyDictionary<string, int> marked)
    {
        var paths = unmerged.Where(p => !marked.ContainsKey(p)).Select(p => new BaseMergeOpenPath(p, BaseMergeOpenPath.Unmerged))
            .Concat(marked.Keys.Select(p => new BaseMergeOpenPath(p, BaseMergeOpenPath.Markers)))
            .OrderBy(p => p.Path, StringComparer.Ordinal).ToList();
        return new BaseMergeOpen(paths, marked.Values.Sum());
    }

    private Task<IReadOnlyList<string>?> Unmerged(ISandbox sandbox, CancellationToken ct) =>
        git.LinesAsync(sandbox, ["diff", "--name-only", "--diff-filter=U"], ct);

    // The paths the merge gave content of its own: their staged blob is neither this branch's nor the base's.
    private async Task<IReadOnlyList<string>?> ProducedAsync(ISandbox sandbox, CancellationToken ct)
    {
        var fromOurs = await git.LinesAsync(sandbox, ["diff", "--cached", "--name-only", "HEAD"], ct);
        var fromTheirs = await git.LinesAsync(sandbox, ["diff", "--cached", "--name-only", "MERGE_HEAD"], ct);
        return fromOurs is null || fromTheirs is null ? null : [.. fromOurs.Intersect(fromTheirs, StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> Union(params IReadOnlyList<string>[] sets) =>
        [.. sets.SelectMany(s => s).Distinct(StringComparer.Ordinal)];
}
