namespace AgentSmith.Contracts.Events;

/// <summary>
/// Sandbox command launched. <see cref="ArgsLength"/> is metadata only — the
/// raw args may carry sensitive content (SQL, secrets, file paths) and are
/// kept out of the event stream by design, matching the prompt-content
/// boundary from p0169e.
/// </summary>
public sealed record SandboxCommandEvent(
    string RunId,
    string Repo,
    string Command,
    int ArgsLength,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxCommand, Timestamp);

/// <summary>
/// Live sandbox stdout/stderr line. Intentionally carries content — the L3
/// expansion gate (ExpandSandbox SignalR group) is the boundary, not the
/// payload. Operators expanding a sandbox have asked for the stream.
/// </summary>
public sealed record SandboxOutputEvent(
    string RunId,
    string Repo,
    string Stream,
    string Line,
    long BatchSeq,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxOutput, Timestamp);

public sealed record SandboxResultEvent(
    string RunId,
    string Repo,
    string Command,
    int ExitCode,
    long DurationMs,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SandboxResult, Timestamp);
