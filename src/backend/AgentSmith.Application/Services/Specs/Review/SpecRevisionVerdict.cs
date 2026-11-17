using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-17-ec48a: what the one review of a master's spec revision answered. <paramref name="Text"/> is the text to
/// write when <see cref="SpecRevisionOutcome.Cleared"/> (the cuts applied), else the text the master sent;
/// <paramref name="Merged"/> is null when no reviewer was asked; <paramref name="Answers"/> are the reviewers' own,
/// for the round record.
/// </summary>
public sealed record SpecRevisionVerdict(
    SpecRevisionOutcome Outcome,
    SpecDraft Text,
    SpecReviewMerged? Merged,
    IReadOnlyList<SpecAttackResult?> Answers,
    SpecRoundEvidenceResult Evidence,
    string? CutRefusal = null)
{
    public bool IsCleared => Outcome == SpecRevisionOutcome.Cleared;
}
