using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-9dc0a: the server's check of a conflict pass — whether the merge is whole, whatever the master said. It
/// stages what the master resolved: the paths its file tools wrote that carry no conflict marker, and a path git still
/// lists unmerged that is gone from disk (a removal). A path still unmerged that the master neither wrote nor staged stays
/// unmerged — a marker file keeps its index stages for --ours/--theirs, and a conflict git left without markers (binary,
/// modify/delete, a merge driver) is never taken silently. Open is every path still unmerged, and every path carrying a
/// marker among the conflicted and the written ones (a path git merged by itself gains no marker). With stage=false it
/// only reads — the baseline before a pass, which must not resolve anything on the master's behalf.
/// </summary>
public sealed class BaseMergeWholeness(BaseRefreshGit git)
{
    public async Task<BaseMergeOpen> CheckAsync(ISandbox sandbox, IReadOnlyList<string> conflicted, IReadOnlyList<string> wrote,
        CancellationToken ct, bool stage = true)
    {
        ArgumentNullException.ThrowIfNull(conflicted);
        ArgumentNullException.ThrowIfNull(wrote);
        var unmerged = await Unmerged(sandbox, ct);
        var marked = unmerged is null ? null : await git.MarkedAsync(sandbox, Union(conflicted, wrote, unmerged), ct);
        var missing = unmerged is null ? null : await git.MissingAsync(sandbox, unmerged, ct);
        if (unmerged is null || marked is null || missing is null) return BaseMergeOpen.All(conflicted, BaseMergeOpenPath.Unchecked);
        IReadOnlyList<string> resolved = [.. wrote.Where(p => !marked.ContainsKey(p)).Concat(missing).Distinct(StringComparer.Ordinal)];
        if (stage && !await git.StageAsync(sandbox, resolved, ct)) return BaseMergeOpen.All(conflicted, BaseMergeOpenPath.Unchecked);
        var left = await Unmerged(sandbox, ct);
        var markedNow = left is null ? null : await git.MarkedAsync(sandbox, Union(conflicted, wrote), ct);
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

    private static IReadOnlyList<string> Union(params IReadOnlyList<string>[] sets) =>
        [.. sets.SelectMany(s => s).Distinct(StringComparer.Ordinal)];
}
