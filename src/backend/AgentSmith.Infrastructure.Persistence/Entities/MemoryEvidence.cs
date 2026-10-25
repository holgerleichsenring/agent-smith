namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-25-9324a: one fact about a memory's use — relied on, confirmed, strained or contradicted — per
/// repository and slug. Run, spec, project and ticket are plain values with no foreign key, so deleting the run
/// leaves the fact standing: a memory's track record is worth keeping longer than any run it came from.
/// </summary>
public sealed class MemoryEvidence : EntityBase
{
    public long Id { get; set; }

    /// <summary>RepoKey.Of the repository's configured URL — the store the memory lives in.</summary>
    public string RepoKey { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    /// <summary>One of MemoryEvidenceKinds.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>What the fact is worth to the standing before it fades; MemoryStandingWeights.</summary>
    public double Weight { get; set; }

    public string Project { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string SpecId { get; set; } = string.Empty;

    /// <summary>The trail position of the event that wrote the row; null when no event did.</summary>
    public long? EventSeq { get; set; }

    /// <summary>Why — a dispute's or a judgement's words; null for a reliance or an outcome.</summary>
    public string? Reason { get; set; }

    /// <summary>The judged criterion's key on a person's contradiction, so a withdrawal retracts its rows.</summary>
    public string? CriterionKey { get; set; }

    public DateTimeOffset At { get; set; }
}
