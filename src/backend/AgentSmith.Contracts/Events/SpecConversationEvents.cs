using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-9a59g: a hand-back was posted — the comment exactly as the ticket received it, the ticket
/// sentence it quotes, and when. The case is not carried: the published cut is its one writer.
/// </summary>
public sealed record SpecHandedBackEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string Text,
    string? Quote = null)
    : RunEvent(RunId, EventType.SpecHandedBack, Timestamp);

/// <summary>
/// 2026-10-13-9a59g: a derived cut was reviewed — every review, an accepted one too. <see cref="Attempt"/>
/// is the derivation attempt the cut came from (1-based); <see cref="Deliverable"/> is
/// <see cref="CutReviewDeliverables.Accepted"/> or <see cref="CutReviewDeliverables.Refused"/>, and an
/// accepted review carries no findings.
/// </summary>
public sealed record SpecCutReviewedEvent(
    string RunId,
    DateTimeOffset Timestamp,
    int Attempt,
    string Deliverable,
    IReadOnlyList<CutFinding> Findings)
    : RunEvent(RunId, EventType.SpecCutReviewed, Timestamp);

/// <summary>2026-10-13-9a59g: the two outcomes of a cut review, as <see cref="SpecCutReviewedEvent"/> carries them.</summary>
public static class CutReviewDeliverables
{
    public const string Accepted = "accepted";
    public const string Refused = "refused";
}
