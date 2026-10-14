using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: what a design turn's review rounds leave — the proposal to publish (its drafts),
/// every round, the spec ids not reviewed yet (null when none), what still stands, and the reply carrying the
/// published drafts.</summary>
public sealed record ProposalReviewOutcome(
    OutcomeProposal Proposal,
    IReadOnlyList<ProposalReviewRound> Rounds,
    IReadOnlyList<string>? NotReviewed,
    IReadOnlyList<ProposalFinding> Standing,
    AgenticLoopResult Reply);
