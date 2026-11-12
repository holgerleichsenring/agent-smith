using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the draft a proposal stands as, so a turn that revises the proposal awaiting a decision rewrites
/// one item of it instead of writing it again whole.
/// </summary>
public static class ProposalDraftSeed
{
    public static ProposalDraft Of(OutcomeProposal proposal) => proposal switch
    {
        SpecOutcome spec => new() { Specs = [new DraftSpec(spec.Draft, false)] },
        EpicOutcome epic => new()
        {
            Specs = [new DraftSpec(epic.Parent, true), .. epic.Children.Select(c => new DraftSpec(c, false))],
            Contract = epic.Contract,
        },
        TicketGraphOutcome graph => new() { Tickets = graph.Tickets },
        BugOutcome bug => new() { Bug = bug.Ticket },
        TicketCommentOutcome comment => new() { Comment = comment.Comment },
        _ => ProposalDraft.Empty,
    };
}
