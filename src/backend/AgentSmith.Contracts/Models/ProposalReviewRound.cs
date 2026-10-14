using AgentSmith.Contracts.Events;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-13-8762i: one review round of one draft of a design turn's proposal, as it stays on the proposal.
/// It keeps its own reviewer verdicts and findings, so a REFUSE a later round fixed still says why.
/// </summary>
/// <param name="SpecId">The draft's spec id, or <see cref="ProposalReviewTokens.Series"/> for a series round.</param>
/// <param name="Round">The round, 1-based.</param>
/// <param name="Verdict">The merged verdict (one of <see cref="SpecReviewVerdicts"/>).</param>
/// <param name="ReviewerVerdicts">Each reviewer's verdict in reviewer order; null where a reviewer was not taken.</param>
/// <param name="Cuts">The round's merged cuts.</param>
/// <param name="Findings">The round's merged findings.</param>
/// <param name="Residues">Cut wording still standing after the design partner re-proposed.</param>
public sealed record ProposalReviewRound(
    string SpecId,
    int Round,
    string Verdict,
    IReadOnlyList<string?> ReviewerVerdicts,
    IReadOnlyList<SpecReviewCut> Cuts,
    IReadOnlyList<SpecReviewFinding> Findings,
    IReadOnlyList<SpecReviewResidue> Residues);
