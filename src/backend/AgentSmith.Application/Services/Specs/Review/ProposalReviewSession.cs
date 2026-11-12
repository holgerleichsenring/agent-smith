using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one design turn's proposal review — the proposal and every draft's review.
/// 2026-11-12-cd4ch: one pass, nothing revised, so the proposal is the one the turn wrote.</summary>
internal sealed class ProposalReviewSession(
    ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, List<ProposalDraftReview> drafts)
{
    public ProposalTurn Turn => turn;
    public SpecReviewSettings Settings => settings;
    public List<ProposalDraftReview> Drafts => drafts;
    public OutcomeProposal Proposal => proposal;

    public SpecSet Set => ProposalSpecSet.Of(Proposal);
    public List<ProposalDraftReview> Open => [.. Drafts.Where(draft => !draft.Stopped)];

    /// <summary>What the run's cost cap says when it is spent, or null.</summary>
    public string? CostCapReached() =>
        PipelineCostTracker.GetOrCreate(Turn.Pipeline) is { IsBudgetExhausted: true } tracker
            ? CostCapStop.Describe(tracker)
            : null;

    /// <summary>
    /// 2026-11-10-18cf: which open drafts may be reviewed. Only the run's cost cap stops them, and it is written on
    /// each draft as the reason. A design turn's review has no wall-time rule and no share. A cancel ends the turn.
    /// </summary>
    public List<ProposalDraftReview> Admit(IReadOnlyList<ProposalDraftReview> open)
    {
        if (open.Count == 0 || CostCapReached() is not { } capped) return [.. open];
        foreach (var draft in open)
        {
            draft.Stopped = true;
            draft.NotReviewed = true;
            draft.StoppedBecause = capped;
        }
        return [];
    }
}
