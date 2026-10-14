using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: holds the design partner to a review round's cuts, draft by draft. A re-proposal of
/// another kind, other spec ids or another child order is invalid as a whole (null). Otherwise each changed
/// draft passes <see cref="SpecRevisionGuard"/> with its cuts, or — refused without cuts, or the epic parent
/// the whole-set findings named — <see cref="SpecIdentityRule"/>; any other change is reverted, alone.
/// Everything but the drafts comes from the last valid proposal — 2026-10-13-8762j: the epic's series contract
/// included, which no draft's cut licenses; the published reply text keeps the first reply's contract block.
/// </summary>
public sealed class ProposalReProposalGuard(SpecRevisionGuard guard)
{
    public ProposalGuardResult? Merge(
        OutcomeProposal last, OutcomeProposal reply, IReadOnlyDictionary<string, ProposalDraftLicence> licences,
        bool parentNamed, bool lastRound)
    {
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(licences);
        var (before, after) = (ProposalSpecSet.Drafts(last), ProposalSpecSet.Drafts(reply));
        if (last.GetType() != reply.GetType() || before.Count != after.Count
            || !before.Select(d => d.SpecId).SequenceEqual(after.Select(d => d.SpecId), StringComparer.Ordinal))
            return null;
        var parentId = last is EpicOutcome epic ? epic.Parent.SpecId : null;
        var (kept, reverted) = (new List<SpecDraft>(), new List<string>());
        for (var i = 0; i < before.Count; i++)
        {
            var (old, @new) = (before[i], after[i]);
            var admitted = Same(old, @new) || Admits(old, @new, licences.GetValueOrDefault(old.SpecId),
                parentNamed && old.SpecId == parentId, lastRound);
            kept.Add(admitted ? @new : old);
            if (!admitted) reverted.Add(old.SpecId);
        }
        return new ProposalGuardResult(Rebuild(last, kept), reverted, before.Where((d, i) => !Same(d, after[i])).Any());
    }

    private bool Admits(SpecDraft old, SpecDraft @new, ProposalDraftLicence? licence, bool namedParent, bool lastRound) =>
        licence is { Cuts.Count: > 0 }
            ? guard.Check(old.Yaml, @new.Yaml, licence.Cuts, licence.Problems, lastRound) is null
            : (licence is { Refused: true } || namedParent) && SpecIdentityRule.Holds(old, @new);

    private static bool Same(SpecDraft a, SpecDraft b) => string.Equals(a.Yaml, b.Yaml, StringComparison.Ordinal);

    private static OutcomeProposal Rebuild(OutcomeProposal last, IReadOnlyList<SpecDraft> drafts) => last switch
    {
        SpecOutcome spec => spec with { Draft = drafts[0] },
        EpicOutcome epic => epic with { Parent = drafts[0], Children = [.. drafts.Skip(1)] },
        _ => last,
    };
}
