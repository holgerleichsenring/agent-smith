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
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CacheCreateTokens { get; init; }
    public long CacheReadTokens { get; init; }
    public int ToolCallCount { get; init; }
    public int LlmCallCount { get; init; }
    public long DurationMs { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}
