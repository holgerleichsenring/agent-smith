namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>One pipeline step's record: its index, name, status, duration and result line.</summary>
public sealed class RunStep : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// 2026-08-25-61f1: the trail position of the event that produced this row. The store
    /// holds at most one row per (RunId, EventSeq), so an event projected twice occupies
    /// one row instead of multiplying every total summed from this table. Null on rows
    /// written before this phase — unattributed, never guessed.
    /// </summary>
    public long? EventSeq { get; set; }

    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    // p0344b: the TYPED command name (CommandNames constant, optionally with a
    // ":param" suffix) from StepStartedEvent — the deterministic input the
    // server-side run-story beat derivation maps on. Null on pre-p0344b rows,
    // which serve beats: null (no storybar) instead of a label-based guess.
    public string? CommandName { get; set; }

    /// <summary>
    /// p0466: the derived phase this step belongs to, written by the executing context
    /// that also composes the "p19213a: " prefix on <see cref="StepName"/>. Null on
    /// pre-p0466 rows and on steps that belong to no phase; those rows are NOT
    /// backfilled — a parsed prefix is what this column replaces, and the read path
    /// keeps the regex as a fallback for them alone.
    /// </summary>
    public string? PhaseId { get; set; }

    public string Status { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
    public string? ResultMessage { get; set; }

    /// <summary>
    /// p0404: model time attributed to this step — the summed DurationMs of the
    /// LLM calls made inside it. Accumulated as the calls land, so it survives the
    /// run instead of living only in the volatile broadcaster snapshot.
    /// </summary>
    public long LlmMs { get; set; }

    /// <summary>
    /// p0404: the slice of <see cref="LlmMs"/> that was spent queueing on the
    /// client-side token rate limiter, not on the model. A SUBSET of LlmMs (the
    /// wait happens inside the measured call), never an addend.
    /// </summary>
    public long ThrottleWaitMs { get; set; }

    /// <summary>
    /// p0404: summed wall time of the sandbox commands this step ran. Against
    /// the step's own duration it also answers whether N commands ran one after
    /// another: sum close to wall-clock means serial execution.
    /// </summary>
    public long SandboxMs { get; set; }
}
