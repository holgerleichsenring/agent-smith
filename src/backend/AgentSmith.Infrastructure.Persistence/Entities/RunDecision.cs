namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A decision the agent logged during the run (category, name, reason).</summary>
public sealed class RunDecision : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// 2026-08-25-61f1: the trail position of the event that produced this row. The store
    /// holds at most one row per (RunId, EventSeq), so a decision projected twice is
    /// recorded once. Null on rows written before this phase.
    /// </summary>
    public long? EventSeq { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>
    /// p0388c: the decision's category, as the producer classified it on
    /// DecisionLoggedEvent. The event always carried it; the projection did not,
    /// so the operator-facing notes lost the qualifier the moment they moved off
    /// the live event buffer. Null on rows written before p0388c, which render
    /// without the category segment rather than inventing one.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// p0388a: the pipeline step the decision was logged in. Null on pre-p0388a
    /// rows — unattributed, never guessed.
    /// </summary>
    public int? StepIndex { get; set; }

    /// <summary>
    /// p0466: the derived phase the decision was taken in, from the producer's ambient
    /// step frame. Null on pre-p0466 rows and outside any phase — unattributed, never
    /// derived from the step index it happens to share with one.
    /// </summary>
    public string? PhaseId { get; set; }
}
