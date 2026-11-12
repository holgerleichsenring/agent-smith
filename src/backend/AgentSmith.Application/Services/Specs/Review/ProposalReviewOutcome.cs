using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: what a design turn's review leaves — the proposal it reviewed, every round, the spec
/// ids not reviewed (null when none) and what still stands. 2026-11-12-cd4ch: no reply; nothing was re-prompted.</summary>
public sealed record ProposalReviewOutcome(
    OutcomeProposal Proposal,
    IReadOnlyList<ProposalReviewRound> Rounds,
    IReadOnlyList<string>? NotReviewed,
    IReadOnlyList<ProposalFinding> Standing)
{
    /// <summary>2026-11-10-8e59: the drafts a review ended clean on, each bound to its exact text.</summary>
    public IReadOnlyList<Contracts.Specs.ReviewedText> ReviewedClean { get; init; } = [];
}
