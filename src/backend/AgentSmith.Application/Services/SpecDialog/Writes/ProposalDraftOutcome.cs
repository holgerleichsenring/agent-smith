using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: the outcome a design turn proposes, derived from its store. A turn that changed nothing is an
/// answer; otherwise the draft's one kind is the proposal: a comment, a bug, tickets, one spec, or a parent with its
/// children and the contract narrowed to them. An emptied draft proposes nothing.
/// </summary>
public static class ProposalDraftOutcome
{
    public static OutcomeProposal For(ProposalStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Changed ? Of(store.Current) : new AnswerOutcome();
    }

    public static OutcomeProposal Of(ProposalDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Comment is { } comment) return new TicketCommentOutcome(comment);
        if (draft.Bug is { } bug) return new BugOutcome(bug);
        if (draft.Tickets.Count > 0) return new TicketGraphOutcome(draft.Tickets);
        var children = draft.Specs.Where(s => !s.Parent).Select(s => s.Draft).ToList();
        if (draft.Specs.FirstOrDefault(s => s.Parent) is { } parent && children.Count > 0)
            return new EpicOutcome(parent.Draft, children, draft.Contract?.Within(children.Select(c => c.SpecId)).Contract);
        return draft.Specs.Count == 1 ? new SpecOutcome(draft.Specs[0].Draft) : new AnswerOutcome();
    }
}
