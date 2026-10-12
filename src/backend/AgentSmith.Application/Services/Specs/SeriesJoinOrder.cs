using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: puts the specs of several sub-series into one order. A spec id is the series'
/// base plus a letter, so the joined order is the id order; <c>requires:</c> is only CHECKED against
/// it — an edge to a spec that comes later (a cycle has one) is refused, as is a done/ set that is
/// not a prefix of the order, because what ran must be the head of what is left to run. One
/// sub-series keeps its manifest's order and is not re-checked: that is how it read before.
/// </summary>
public sealed class SeriesJoinOrder
{
    public (IReadOnlyList<SpecPhase> Phases, IReadOnlyList<string> Executed, string? Why) Of(
        IReadOnlyList<SubSeriesRead> joined)
    {
        ArgumentNullException.ThrowIfNull(joined);
        if (joined.Count == 1) return (joined[0].Phases, joined[0].Executed, null);
        var ordered = joined.SelectMany(r => r.Phases).OrderBy(p => p.PhaseId, StringComparer.Ordinal).ToList();
        var executed = joined.SelectMany(r => r.Executed).ToHashSet(StringComparer.Ordinal);
        var why = Duplicate(joined) ?? BackwardEdge(ordered) ?? Gap(ordered, executed);
        return why is not null
            ? ([], [], why)
            : (ordered, [.. ordered.Select(p => p.PhaseId).Where(executed.Contains)], null);
    }

    private static string? Duplicate(IReadOnlyList<SubSeriesRead> joined) =>
        joined.SelectMany(r => r.Phases.Select(p => (Id: p.PhaseId, r.Repo)))
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1) is { } twice
            ? $"spec {twice.Key} lies in more than one repository: {string.Join(", ", twice.Select(x => x.Repo))}"
            : null;

    private static string? BackwardEdge(IReadOnlyList<SpecPhase> ordered)
    {
        var position = ordered.Select((p, i) => (p.PhaseId, i)).ToDictionary(x => x.PhaseId, x => x.i, StringComparer.Ordinal);
        for (var i = 0; i < ordered.Count; i++)
            foreach (var required in ordered[i].Draft.Requires)
                if (position.TryGetValue(required, out var at) && at >= i)
                    return $"spec {ordered[i].PhaseId} requires {required}, which does not come before it in id order";
        return null;
    }

    private static string? Gap(IReadOnlyList<SpecPhase> ordered, HashSet<string> executed)
    {
        var firstOpen = ordered.FirstOrDefault(p => !executed.Contains(p.PhaseId));
        if (firstOpen is null) return null;
        var after = ordered.SkipWhile(p => p != firstOpen).FirstOrDefault(p => executed.Contains(p.PhaseId));
        return after is null ? null
            : $"spec {after.PhaseId} lies in done/ while {firstOpen.PhaseId} before it does not — the executed specs are not a prefix of the series";
    }
}
