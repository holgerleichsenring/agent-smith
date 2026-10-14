using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59l: the dispositions an approval recorded, applied by id to the series a branch holds.
/// A re-cut replaces an unstarted spec and keeps its id and branch label; a drop removes an unstarted
/// one; an appended spec takes the next free member id past every id on the branch. A spec that ran
/// since the approval's basis is kept as it ran, and its re-cut or drop is reported as discarded.
/// Branch specs the basis never saw stay after the appended ones and are named.
/// </summary>
public sealed class ContinuationMerge(SpecDraftIdRewriter rewriter)
{
    public (IReadOnlyList<DerivedSpec> Specs, IReadOnlyList<string> Notes) Apply(
        SpecSet branch, SpecSet approved, SeriesContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(approved);
        ArgumentNullException.ThrowIfNull(continuation);
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var disposition in continuation.Dispositions) kinds.TryAdd(disposition.SpecId, disposition.Kind);
        var approvedById = approved.Specs.GroupBy(s => s.SpecId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var notes = new List<string>();
        var seen = new List<DerivedSpec>();
        var unseen = new List<DerivedSpec>();
        foreach (var spec in branch.Specs)
        {
            if (!kinds.TryGetValue(spec.SpecId, out var kind) || kind == SpecDispositionKinds.Appended)
            {
                unseen.Add(spec);
                continue;
            }
            var ran = branch.Executed.Contains(spec.SpecId, StringComparer.Ordinal);
            if (ran && kind is SpecDispositionKinds.Recut or SpecDispositionKinds.Dropped)
                notes.Add($"spec {spec.SpecId} ran since the approval was given, so it is kept as it ran and "
                    + $"the approval's {(kind == SpecDispositionKinds.Recut ? "re-cut" : "drop")} of it is discarded");
            else if (kind == SpecDispositionKinds.Dropped) continue;
            else if (kind == SpecDispositionKinds.Recut && approvedById.TryGetValue(spec.SpecId, out var recut))
            {
                seen.Add(spec with { Draft = recut.Draft });
                continue;
            }
            seen.Add(spec);
        }
        if (unseen.Count > 0)
            notes.Add($"spec(s) {string.Join(", ", unseen.Select(s => s.SpecId))} reached the branch after the "
                + "approval was given and stay after the approved ones");
        return ([.. seen, .. Appended(branch, approved, continuation, approvedById), .. unseen], notes);
    }

    private IReadOnlyList<DerivedSpec> Appended(
        SpecSet branch, SpecSet approved, SeriesContinuation continuation,
        IReadOnlyDictionary<string, DerivedSpec> approvedById)
    {
        var series = branch.Series ?? approved.Series ?? string.Empty;
        var taken = new HashSet<string>(branch.Specs.Select(s => s.SpecId), StringComparer.Ordinal);
        foreach (var id in branch.Executed) taken.Add(id);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var appended = new List<DerivedSpec>();
        foreach (var disposition in continuation.Dispositions.Where(d => d.Kind == SpecDispositionKinds.Appended))
        {
            if (!approvedById.TryGetValue(disposition.SpecId, out var spec) || ids.ContainsKey(spec.SpecId)) continue;
            var id = taken.Contains(spec.SpecId) ? SeriesIdFactory.NextFree(series, 0, taken).Id : spec.SpecId;
            taken.Add(id);
            ids[spec.SpecId] = id;
            appended.Add(spec);
        }
        var moved = ids.Where(p => p.Key != p.Value).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return [.. appended.Select(spec => spec with
        {
            Draft = spec.Draft with
            {
                SpecId = rewriter.RewriteValue(spec.Draft.SpecId, moved),
                Requires = [.. spec.Draft.Requires.Select(r => rewriter.RewriteValue(r, moved))],
                Yaml = rewriter.Rewrite(spec.Draft.Yaml, moved),
            },
        })];
    }
}
