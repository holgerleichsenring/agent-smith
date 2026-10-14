using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-8762l: one review round of one spec or of the series. A run round carries each reviewer's own
/// answer and leaves the round-level lists empty; a conversation round (scope <see cref="Conversation"/>) carries
/// the merged cuts, findings and residues at round level and its reviewers' verdicts only.
/// </summary>
/// <param name="Scope"><c>spec</c>, <c>series</c> or <c>conversation</c>.</param>
/// <param name="SpecId">The spec; null on a run's series round; <c>series</c> for the conversation's series round.</param>
/// <param name="Verdict">The merged verdict — the most severe a reviewer gave.</param>
public sealed record TicketReviewRoundView(
    string Scope,
    string? SpecId,
    int Round,
    string Verdict,
    IReadOnlyList<TicketReviewerView> Reviewers,
    IReadOnlyList<TicketReviewCutView> Cuts,
    IReadOnlyList<TicketReviewFindingView> Findings,
    IReadOnlyList<SpecReviewResidue> Residues,
    int? RevisionNumber,
    DateTimeOffset? At)
{
    public const string Conversation = "conversation";
}
