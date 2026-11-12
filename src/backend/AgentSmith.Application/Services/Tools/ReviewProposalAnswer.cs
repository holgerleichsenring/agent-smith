using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4cj: what review_proposal returns — data, never an instruction. <paramref name="Note"/> says
/// why nothing was reviewed, or is null; the rest is the pass: every finding under its item, each draft's verdict with
/// its suggested cuts, and the ids the pass could not review.</summary>
public sealed record ReviewProposalAnswer(
    string? Note,
    IReadOnlyList<ProposalFinding> Findings,
    IReadOnlyList<ProposalReviewRound> Verdicts,
    IReadOnlyList<string> NotReviewed)
{
    public static ReviewProposalAnswer Nothing(string note) => new(note, [], [], []);
}
