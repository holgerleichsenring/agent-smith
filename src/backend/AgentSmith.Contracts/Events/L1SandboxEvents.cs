namespace AgentSmith.Contracts.Events;

// p0332: MemoryRequest is an additive trailing optional (same pattern as
// LlmCallFinishedEvent's cache fields in p0323) — events persisted before p0332
// deserialise with null. It carries the sandbox pod's Kubernetes memory-request
// quantity (e.g. "1Gi") so the projection can compute reserved resource-time
// (request x lifetime) per run. Null means "producer didn't say"; consumers
// fall back to the platform default request.
public sealed record SandboxCreatedEvent(
    string RunId,
    string Repo,
    string Image,
    string? Language,
    DateTimeOffset Timestamp,
    string? MemoryRequest = null)
    : RunEvent(RunId, EventType.SandboxCreated, Timestamp);

public sealed record SandboxDisposedEvent(
    string RunId,
    string Repo,
    int? ExitCode,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxDisposed, Timestamp);
