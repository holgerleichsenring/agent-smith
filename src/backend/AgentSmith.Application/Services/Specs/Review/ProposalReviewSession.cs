using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one design turn's proposal review between rounds — the last valid proposal and the
/// reply it came from, and every draft's review.</summary>
internal sealed class ProposalReviewSession(
    ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, AgenticLoopResult firstReply,
    IReadOnlyList<ProposalDraftReview> drafts)
{
    public ProposalTurn Turn => turn;
    public SpecReviewSettings Settings => settings;
    public AgenticLoopResult FirstReply => firstReply;
    public IReadOnlyList<ProposalDraftReview> Drafts => drafts;
    public OutcomeProposal Proposal { get; set; } = proposal;
    public ProposalReply LastReply { get; set; } = new(firstReply, proposal);
    public bool ParentNamed { get; init; }
    public int Round { get; set; }
    public IReadOnlyList<SpecDraft> Reverted { get; set; } = [];

    public bool LastRound => Round == Settings.Review.Rounds;
    public SpecSet Set => ProposalSpecSet.Of(Proposal);
    public List<ProposalDraftReview> Open => [.. Drafts.Where(draft => !draft.Stopped)];

    /// <summary>What the run's cost cap says when it is spent, or null.</summary>
    public string? CostCapReached() =>
        PipelineCostTracker.GetOrCreate(Turn.Pipeline) is { IsBudgetExhausted: true } tracker
            ? CostCapStop.Describe(tracker)
            : null;

    /// <summary>
    /// 2026-11-10-18cf: which open drafts may still be reviewed, asked before every round and every re-prompt.
    /// Only the run's cost cap stops them, and it is written on each draft as the reason. A design turn's review
    /// has no wall-time rule and no share: time says nothing about a review's quality, and a spec handed out
    /// unreviewed is worse than a slow turn. A cancel ends the turn itself.
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
