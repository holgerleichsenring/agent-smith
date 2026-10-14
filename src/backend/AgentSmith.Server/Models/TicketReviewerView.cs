using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-8762l: one reviewer's answer in a round, 0-based; a conversation round's reviewer carries
/// its verdict only, and a null verdict where it was not taken.</summary>
public sealed record TicketReviewerView(
    int Reviewer,
    string? Verdict,
    IReadOnlyList<TicketReviewCutView> Cuts,
    IReadOnlyList<TicketReviewFindingView> Findings,
    IReadOnlyList<SpecEvidenceProblem> EvidenceProblems,
    IReadOnlyList<SpecReviewResidue> Residues);
