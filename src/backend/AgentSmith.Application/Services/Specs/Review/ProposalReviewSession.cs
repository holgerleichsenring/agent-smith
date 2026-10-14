using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one design turn's proposal review between rounds — the last valid proposal and the
/// reply it came from, every draft's review, the floor draft the share never stops, and the round's clock.</summary>
internal sealed class ProposalReviewSession(
    ProposalTurn turn, SpecReviewSettings settings, OutcomeProposal proposal, AgenticLoopResult firstReply,
    IReadOnlyList<ProposalDraftReview> drafts, TimeProvider clock)
{
    public ProposalTurn Turn => turn;
    public SpecReviewSettings Settings => settings;
    public TimeProvider Clock => clock;
    public AgenticLoopResult FirstReply => firstReply;
    public IReadOnlyList<ProposalDraftReview> Drafts => drafts;
    public OutcomeProposal Proposal { get; set; } = proposal;
    public ProposalReply LastReply { get; set; } = new(firstReply, proposal);
    public ProposalDraftReview? Floor { get; set; }
    public bool ParentNamed { get; init; }
    public int Round { get; set; }
    public TimeSpan LastRoundDuration { get; set; }
    public IReadOnlyList<SpecDraft> Reverted { get; set; } = [];

    public bool LastRound => Round == Settings.Review.Rounds;
    public SpecSet Set => ProposalSpecSet.Of(Proposal);
    public List<ProposalDraftReview> Open => [.. Drafts.Where(draft => !draft.Stopped)];
}
