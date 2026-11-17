using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal as a spec set — one <see cref="DerivedSpec"/> per draft, nothing
/// executed, no revisions — so <see cref="SpecToday"/> reads a child's requires on a sibling as unbuilt.
/// 2026-11-17-5701a: a ticket list's set is every ticket's specs.
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

    /// <summary>Every draft a proposal states: a spec's draft, an epic's parent and children, every ticket's specs.</summary>
    public static IReadOnlyList<SpecDraft> Drafts(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => [spec.Draft],
        EpicOutcome epic => [epic.Parent, .. epic.Children],
        TicketGraphOutcome graph => [.. graph.Tickets.SelectMany(t => t.Specs)],
        _ => [],
    };

    /// <summary>2026-11-17-5701a: the ticket of a list whose spec <paramref name="specId"/> is, or null.</summary>
    public static GraphTicket? TicketOf(OutcomeProposal proposal, string specId) =>
        (proposal as TicketGraphOutcome)?.Tickets.FirstOrDefault(t => t.Specs.Any(s => s.SpecId == specId));
}
