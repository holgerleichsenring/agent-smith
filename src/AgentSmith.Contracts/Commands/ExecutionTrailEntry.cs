namespace AgentSmith.Contracts.Commands;

/// <summary>
/// A single entry in the pipeline execution trail, tracking command execution metadata.
/// </summary>
public sealed record ExecutionTrailEntry(
    string CommandName,
    string? Skill,
    bool Success,
    string Message,
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    int? InsertedCommandCount);
