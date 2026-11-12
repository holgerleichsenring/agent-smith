using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: whether the draft's specs still form one proposal once <c>spec</c> is placed — one spec, or one
/// parent with children whose requires name siblings without a cycle. Several specs without a parent are refused,
/// so the parent is written first; the refusal says how to go on.
/// </summary>
public sealed class SpecDraftShape(RequiresEdgeChecker edges)
{
    public DraftWrite Place(ProposalDraft draft, DraftSpec spec)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(spec);
        List<DraftSpec> specs = [.. draft.Specs];
        var at = specs.FindIndex(s => s.Draft.SpecId == spec.Draft.SpecId);
        if (at >= 0) specs[at] = spec;
        else specs.Add(spec);
        return Refusal(specs, spec) is { } refusal
            ? DraftWrite.Refused(refusal)
            : DraftWrite.To(draft with { Specs = specs });
    }

    private string? Refusal(IReadOnlyList<DraftSpec> specs, DraftSpec written)
    {
        var parents = specs.Where(s => s.Parent).ToList();
        if (parents.Count > 1)
            return $"'{parents.First(p => p != written).Draft.SpecId}' is the parent already — write this spec as a "
                + "child, or rewrite that one without parent";
        var parent = parents.SingleOrDefault();
        if (parent is null)
            return specs.Count > 1
                ? $"the draft holds the spec '{specs[0].Draft.SpecId}' already; several specs are an epic — write its "
                    + "parent with parent: true, then each child"
                : null;
        return edges.Check(parent.Draft, [.. specs.Where(s => !s.Parent).Select(s => s.Draft)]);
    }
}
