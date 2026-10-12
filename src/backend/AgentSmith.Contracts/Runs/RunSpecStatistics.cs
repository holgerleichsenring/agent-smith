namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: what one spec of a ticket cost and how it ended — steps, wall clock, the fold
/// over its calls, and how many of its commands came back non-zero.
/// <para>
/// Every number here is a fold over the recorded trail grouped by spec. None of them is a
/// counter: a counter and the events it counts are two answers to one question.
/// </para>
/// </summary>
/// <param name="PhaseId">The spliced spec id; null for the steps that belong to no spec.</param>
public sealed record RunSpecStatistics(
    string? PhaseId,
    int Steps,
    long DurationMs,
    RunCallStatistics Calls,
    int Commands,
    int FailedCommands);
