using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: holds the design partner to a review round's cuts, draft by draft. Each changed draft passes
/// <see cref="SpecRevisionGuard"/> with its cuts, or — refused without cuts, or the epic parent the whole-set
/// findings named — <see cref="SpecIdentityRule"/>; any other change is reverted, alone. 2026-11-11-2bdfb: an epic's
/// children may be re-cut (<see cref="ProposalEpicRecut"/>), and the reply is void as a whole only for another kind,
/// another parent id, a lone spec's changed id, a dropped id the merged set still requires, or merged requires that
/// do not hold. The series contract is the last valid proposal's unless the id set changed; then it is the reply's.
/// </summary>
public sealed class ProposalReProposalGuard(SpecRevisionGuard guard)
{
    public const string OtherKind = "the re-proposal is another kind of outcome than the one under review";
    public const string OtherParent = "the re-proposal names another epic parent";
    public const string OtherId = "the re-proposal changes the spec's id";

    public static string StillRequired(string dropped, string by) =>
        $"the re-proposal drops {dropped}, which {by} still requires";

    public static string BrokenEdges(string problem) => $"the re-cut set's requires do not hold: {problem}";

    public ProposalGuardResult Merge(
        OutcomeProposal last, OutcomeProposal reply, IReadOnlyDictionary<string, ProposalDraftLicence> licences,
        bool parentNamed, bool lastRound)
    {
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(licences);
        if (last.GetType() != reply.GetType()) return ProposalGuardResult.Voided(last, OtherKind);
        if (last is EpicOutcome before && reply is EpicOutcome after)
            return Epic(before, after, licences, parentNamed, lastRound);
        var (was, now) = (ProposalSpecSet.Drafts(last), ProposalSpecSet.Drafts(reply));
        if (!was.Select(d => d.SpecId).SequenceEqual(now.Select(d => d.SpecId), StringComparer.Ordinal))
            return ProposalGuardResult.Voided(last, OtherId);
        var kept = was.Select((old, i) => Kept(old, now[i], licences, namedParent: false, lastRound)).ToList();
        return new ProposalGuardResult(last is SpecOutcome spec && kept.Count > 0 ? spec with { Draft = kept[0] } : last,
            [.. was.Where((old, i) => !ReferenceEquals(kept[i], now[i])).Select(d => d.SpecId)],
            was.Where((d, i) => !ProposalEpicRecut.Same(d, now[i])).Any());
    }

    private ProposalGuardResult Epic(
        EpicOutcome last, EpicOutcome reply, IReadOnlyDictionary<string, ProposalDraftLicence> licences,
        bool parentNamed, bool lastRound)
    {
        if (last.Parent.SpecId != reply.Parent.SpecId) return ProposalGuardResult.Voided(last, OtherParent);
        var parent = Kept(last.Parent, reply.Parent, licences, parentNamed, lastRound);
        var children = ProposalEpicRecut.Merge(last.Children, reply.Children, licences,
            (old, @new) => Admits(old, @new, licences.GetValueOrDefault(old.SpecId), namedParent: false, lastRound));
        if (Problem(parent, children) is { } why) return ProposalGuardResult.Voided(last, why);
        var setChanged = children.Added.Count > 0 || children.Dropped.Count > 0;
        var changed = !ProposalEpicRecut.Same(last.Parent, reply.Parent)
            || !last.Children.Select(c => c.Yaml).SequenceEqual(reply.Children.Select(c => c.Yaml), StringComparer.Ordinal);
        IReadOnlyList<string> reverted = ReferenceEquals(parent, reply.Parent)
            ? children.Reverted : [last.Parent.SpecId, .. children.Reverted];
        return new ProposalGuardResult(
            last with { Parent = parent, Children = children.Kept, Contract = setChanged ? reply.Contract : last.Contract },
            reverted, changed) { Added = children.Added, Dropped = children.Dropped };
    }

    private static string? Problem(SpecDraft parent, ProposalChildMerge children)
    {
        var dropped = children.Dropped.ToHashSet(StringComparer.Ordinal);
        foreach (var draft in children.Kept.Prepend(parent))
            if (draft.Requires.FirstOrDefault(dropped.Contains) is { } gone) return StillRequired(gone, draft.SpecId);
        return new RequiresEdgeChecker().Check(parent, children.Kept) is { } problem ? BrokenEdges(problem) : null;
    }

    private SpecDraft Kept(
        SpecDraft old, SpecDraft @new, IReadOnlyDictionary<string, ProposalDraftLicence> licences, bool namedParent,
        bool lastRound) =>
        ProposalEpicRecut.Same(old, @new) || Admits(old, @new, licences.GetValueOrDefault(old.SpecId), namedParent, lastRound)
            ? @new : old;

    private bool Admits(SpecDraft old, SpecDraft @new, ProposalDraftLicence? licence, bool namedParent, bool lastRound) =>
        licence is { Cuts.Count: > 0 }
            ? guard.Check(old.Yaml, @new.Yaml, licence.Cuts, licence.Problems, lastRound) is null
            : (licence is { Refused: true } || namedParent) && SpecIdentityRule.Holds(old, @new);
}
