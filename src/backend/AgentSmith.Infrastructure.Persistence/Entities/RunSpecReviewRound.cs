namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-8762e: one reviewer's answer in one review round of a spec or a series, as a code run took
/// it. Written only by the event-87 projection, one row per event, identified by its trail position; kept
/// as long as the run is. The cuts, findings, evidence problems and residues are the event's lists as JSON.
/// </summary>
public sealed class RunSpecReviewRound : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The trail position of the event; null when it was applied outside the drain.</summary>
    public long? EventSeq { get; set; }

    /// <summary>The spec reviewed; null on a series round.</summary>
    public string? SpecId { get; set; }

    /// <summary><c>spec</c> or <c>series</c>.</summary>
    public string Scope { get; set; } = string.Empty;

    public int Round { get; set; }

    /// <summary>The reviewer within the round, 0-based.</summary>
    public int Reviewer { get; set; }

    /// <summary><c>build</c>, <c>build-with-cuts</c> or <c>refuse</c>.</summary>
    public string Verdict { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
    public string CutsJson { get; set; } = "[]";
    public string FindingsJson { get; set; } = "[]";
    public string EvidenceProblemsJson { get; set; } = "[]";
    public string ResiduesJson { get; set; } = "[]";

    /// <summary>The set revision the round reviewed, when its caller knew it.</summary>
    public int? RevisionNumber { get; set; }
}
