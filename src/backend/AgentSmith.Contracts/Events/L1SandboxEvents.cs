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

// 2026-10-13-9a59i: ExitState is what the backend read before the release — 'running', 'exited'
// or 'gone' (SandboxExitLabels); null when the backend has no process to read. ExitCode and
// OomKilled are set only when it exited.
public sealed record SandboxDisposedEvent(
    string RunId,
    string Repo,
    int? ExitCode,
    DateTimeOffset Timestamp,
    string? ExitState = null,
    bool? OomKilled = null)
    : RunEvent(RunId, EventType.SandboxDisposed, Timestamp);
