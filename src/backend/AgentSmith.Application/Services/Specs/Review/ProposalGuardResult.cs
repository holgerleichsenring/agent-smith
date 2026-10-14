using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: a re-proposal merged onto the last valid proposal — the drafts the guard
/// admitted, the ids it reverted to their last valid text, and whether the reply changed any draft at all.</summary>
public sealed record ProposalGuardResult(OutcomeProposal Merged, IReadOnlyList<string> Reverted, bool Changed);
