using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: whether the draft's specs still form one proposal once <c>spec</c> is placed — one spec, or one
/// parent with children whose requires name siblings without a cycle. Several specs without a parent are refused,
/// so the parent is written first; the refusal says how to go on. 2026-11-15-06fc: the parent is the file under
/// epic/, a spec under specs/; one id is never both, and a requirement naming no file of the draft says which file
/// to write.
/// </summary>
public sealed class SpecDraftShape(RequiresEdgeChecker edges)
{
    public DraftWrite Place(ProposalDraft draft, DraftSpec spec)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(spec);
        List<DraftSpec> specs = [.. draft.Specs];
        var at = specs.FindIndex(s => s.Draft.SpecId == spec.Draft.SpecId);
        if (at >= 0 && specs[at].Parent != spec.Parent)
            return DraftWrite.Refused($"{Shown(specs[at])} holds '{spec.Draft.SpecId}' already — delete it first, or "
                + "give this spec another id");
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
            return $"{Shown(parents.First(p => p != written))} is the epic's parent already — an epic has one parent; "
                + $"write this spec under {DraftPath.Prefix}{DraftPath.Specs}";
        var parent = parents.SingleOrDefault();
        if (parent is null)
            return specs.Count > 1
                ? $"the draft holds {Shown(specs[0])} already; several specs are an epic — write its parent as "
                    + $"{DraftPath.Prefix}{DraftPath.Epic}<id>.yaml first"
                : null;
        var children = specs.Where(s => !s.Parent).Select(s => s.Draft).ToList();
        return Missing(children, parent.Draft.SpecId) ?? edges.Check(parent.Draft, children);
    }

    private static string? Missing(IReadOnlyList<SpecDraft> children, string parentId)
    {
        var ids = children.Select(c => c.SpecId).Append(parentId).ToHashSet(StringComparer.Ordinal);
        foreach (var child in children)
            foreach (var id in child.Requires.Where(r => RequiresEdgeChecker.SpecIdRegex().IsMatch(r) && !ids.Contains(r)))
                return $"'{child.SpecId}' requires '{id}', which no spec of this draft is — write "
                    + $"{DraftPath.Prefix}{DraftPath.OfSpec(id, false)} first, or state an outside precondition as a sentence";
        return null;
    }

    private static string Shown(DraftSpec spec) => DraftPath.Shown(DraftPath.OfSpec(spec.Draft.SpecId, spec.Parent));
}
