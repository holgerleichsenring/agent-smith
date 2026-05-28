namespace AgentSmith.Application.Models;

/// <summary>
/// Per-skill-call cost + timing record. Aggregated by PipelineCostTracker
/// into PerSkillBreakdown alongside the existing pipeline-total properties.
/// </summary>
public sealed record CallCostRecord
{
    public required string SkillName { get; init; }
    public required string Role { get; init; }
    public required SkillExecutionPhase Phase { get; init; }

    /// <summary>
    /// p0176a: per-repo attribution for multi-repo runs. Null when no
    /// repo-scoped caller opened the scope (single-repo runs, legacy
    /// handler paths). PipelineCostTracker.BuildSummary groups by repo
    /// when any record has this set.
    /// </summary>
    public string? RepoName { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CacheCreateTokens { get; init; }
    public long CacheReadTokens { get; init; }
    public int ToolCallCount { get; init; }
    public int LlmCallCount { get; init; }
    public long DurationMs { get; init; }
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// p0142: which LimitEnforcer cap (if any) terminated the call. Values:
    /// 'tokens', 'wall-clock', 'tool-calls', 'llm-calls'. Null when the call
    /// completed without hitting any limit. Operator-visible in the
    /// PerSkillBreakdown table's Limit column.
    /// </summary>
    public string? HitLimit { get; init; }
}
