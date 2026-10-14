namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59e: one publication of a run's spec set — the cut as the attempt published it. The
/// branch stays the content of record; this row is what THIS attempt published, so a ticket's series
/// reads without a clone and dies with its run. One row per publish event, identified by its trail
/// position like every row an event inserts.
/// </summary>
public sealed class RunSeriesCut : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The trail position of the publish event; null when it was applied outside the drain.</summary>
    public long? EventSeq { get; set; }

    /// <summary>The set's key — the ticket key on a hand-back that never reached the publisher.</summary>
    public string SetKey { get; set; } = string.Empty;

    /// <summary>The hand-back case (SpecHandbackCase name); null for a set that was not handed back.</summary>
    public string? HandbackCase { get; set; }

    /// <summary>The published specs as a JSON array of SpecCutEntry; "[]" for a hand-back.</summary>
    public string SpecsJson { get; set; } = "[]";

    public DateTimeOffset At { get; set; }
}
