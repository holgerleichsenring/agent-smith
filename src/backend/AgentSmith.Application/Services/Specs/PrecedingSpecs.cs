using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: the specs of this set that have ALREADY RUN before the current one —
/// in an earlier run (<see cref="SpecSet.Executed"/>) or in this one (the sequence's per-spec
/// table, which CommitSpecWork has already put on the branch).
/// <para>
/// This is the fact a premise check cannot do without. A set is cut as a sequence: slice 3's
/// facts describe the repository as slices 1 and 2 LEAVE it, and by the time slice 3 is entered
/// their work is committed in the sandbox the checker looks into. Without this the checker is
/// asked whether the premises hold "as the repositories are now", sees its own predecessors'
/// changes, and reports a correct spec as resting on something that is no longer so — a false
/// hand-back on exactly the shape this series exists to support.
/// </para>
/// </summary>
internal static class PrecedingSpecs
{
    /// <summary>Spec id and goal for every spec of the set ordered before <paramref name="specId"/>
    /// that has run, in sequence order. Empty for the first spec, and for a run with no set.</summary>
    public static IReadOnlyList<SpecProgress> Of(PipelineContext pipeline, string specId)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) || set is null) return [];
        var order = set.Specs.Select(p => p.SpecId).ToList();
        var at = order.IndexOf(specId);
        if (at <= 0) return [];

        var progress = pipeline.TryGet<SpecSequenceProgress>(
            ContextKeys.SpecSequenceProgress, out var p) && p is not null ? p : null;
        return [.. order.Take(at)
            .Select(id => Standing(set, progress, id))
            .Where(row => row is not null)
            .Select(row => row!)];
    }

    // Done in this run, or executed in an earlier one — either way its work is in the sandbox.
    private static SpecProgress? Standing(
        SpecSet set, SpecSequenceProgress? progress, string id)
    {
        var goal = set.Specs.FirstOrDefault(x => x.SpecId == id)?.Draft.Goal ?? id;
        var row = progress?.Specs.FirstOrDefault(x => x.SpecId == id);
        if (row is { State: SpecRunState.Done }) return row with { Goal = goal };
        return set.Executed.Contains(id, StringComparer.Ordinal)
            ? new SpecProgress(id, goal, SpecRunState.Done)
            : null;
    }
}
