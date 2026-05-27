namespace AgentSmith.Contracts.Events;

public sealed record StepStartedEvent(
    string RunId,
    int StepIndex,
    string StepName,
    int TotalSteps,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.StepStarted, Timestamp);

public sealed record StepFinishedEvent(
    string RunId,
    int StepIndex,
    string Status,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? Reason = null)
    : RunEvent(RunId, EventType.StepFinished, Timestamp);
