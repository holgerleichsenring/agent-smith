using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal as a spec set — one <see cref="DerivedSpec"/> per draft, nothing
/// executed, no revisions — so <see cref="SpecToday"/> reads a child's requires on a sibling as unbuilt.
/// </summary>
public static class ProposalSpecSet
{
    public static SpecSet Of(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return new SpecSet(
            "proposal", [.. Drafts(proposal).Select(draft => new DerivedSpec(draft, string.Empty, string.Empty, []))],
            SpecAccounting.Empty, [], SpecSource.Derived);
    }

    /// <summary>Every draft a proposal states: a spec's draft, an epic's parent and children.</summary>
    public static IReadOnlyList<SpecDraft> Drafts(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => [spec.Draft],
        EpicOutcome epic => [epic.Parent, .. epic.Children],
        _ => [],
    };
}
