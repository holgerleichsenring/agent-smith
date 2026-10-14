using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: what a review round allows a design partner to change in one draft — its cuts
/// with that round's evidence problems, or, refused without a cut, a rewrite under the same identity.</summary>
public sealed record ProposalDraftLicence(
    IReadOnlyList<SpecReviewCut> Cuts, IReadOnlyList<SpecEvidenceProblem> Problems, bool Refused);
