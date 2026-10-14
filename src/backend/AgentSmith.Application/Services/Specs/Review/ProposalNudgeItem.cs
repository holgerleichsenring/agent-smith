using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one draft the design partner is asked to change after a review round — its cuts,
/// or, refused without a cut, the findings it is rewritten from; and its evidence problems.</summary>
public sealed record ProposalNudgeItem(
    SpecDraft Draft,
    IReadOnlyList<SpecReviewCut> Cuts,
    IReadOnlyList<SpecReviewFinding> RefuseFindings,
    IReadOnlyList<SpecEvidenceProblem> Problems);
