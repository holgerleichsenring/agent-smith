namespace AgentSmith.Contracts.Events;

public sealed record RunStartedEvent(
    string RunId,
    string Trigger,
    string Pipeline,
    IReadOnlyList<string> Repos,
    DateTimeOffset StartedAt)
    : RunEvent(RunId, EventType.RunStarted, StartedAt);

public sealed record RunFinishedEvent(
    string RunId,
    string Status,
    string? PrUrl,
    string Summary,
    DateTimeOffset FinishedAt)
    : RunEvent(RunId, EventType.RunFinished, FinishedAt);
