namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>One LLM call's cost + timing record, attributed to a role/phase/model.</summary>
public sealed class RunLlmCall : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// 2026-08-25-61f1: the trail position of the event that produced this row. The store
    /// holds at most one row per (RunId, EventSeq), so a call projected twice is summed
    /// once — the cost rollups read this table. Null on rows written before this phase.
    /// </summary>
    public long? EventSeq { get; set; }

    public string? Role { get; set; }
    public string? Phase { get; set; }
    public string? Model { get; set; }
    public long TokensIn { get; set; }
    public long TokensOut { get; set; }
    public decimal CostUsd { get; set; }
    public long DurationMs { get; set; }
    public string? PromptHash { get; set; }
    /// <summary>p0323: prompt tokens served from the provider's cache (0 = no cache hit).</summary>
    public long CachedTokensIn { get; set; }
    /// <summary>p0323: prompt tokens written to the provider's cache this call.</summary>
    public long CacheCreationTokensIn { get; set; }

    /// <summary>
    /// p0388a: the pipeline step this call was made in, so per-step cost and call
    /// counts are one query. Null on pre-p0388a rows — unattributed, never guessed.
    /// </summary>
    public int? StepIndex { get; set; }
}
