namespace AgentSmith.Contracts.Events;

public sealed record SandboxCreatedEvent(
    string RunId,
    string Repo,
    string Image,
    string? Language,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxCreated, Timestamp);

public sealed record SandboxDisposedEvent(
    string RunId,
    string Repo,
    int? ExitCode,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxDisposed, Timestamp);
