namespace AgentSmith.Contracts.Events;

public sealed record LlmCallStartedEvent(
    string RunId,
    string Model,
    string Role,
    string PromptHash,
    DateTimeOffset Timestamp,
    string? Phase = null,
    string? RepoName = null)
    : RunEvent(RunId, EventType.LlmCallStarted, Timestamp);

public sealed record LlmCallFinishedEvent(
    string RunId,
    string Model,
    string Role,
    long TokensIn,
    long TokensOut,
    decimal CostUsd,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? Phase = null,
    string? RepoName = null)
    : RunEvent(RunId, EventType.LlmCallFinished, Timestamp);
