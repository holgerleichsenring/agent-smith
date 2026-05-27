namespace AgentSmith.Contracts.Events;

/// <summary>
/// Sandbox command launched. <see cref="ArgsLength"/> stays metadata-only
/// for raw arg blobs (SQL, secrets, file content). <see cref="Summary"/> is
/// an optional, producer-curated one-liner (≤120 chars) — operators in a
/// dev-tool dashboard need to know <i>what was launched against what</i>,
/// not just <i>that something ran</i>. Producers must only put
/// operator-visible identifiers (paths, subcommands, target names) in the
/// summary; full arg blobs remain off-stream. Softens the strict
/// metadata-only boundary from p0169e — see decisions/p0175.yaml.
/// </summary>
public sealed record SandboxCommandEvent(
    string RunId,
    string Repo,
    string Command,
    int ArgsLength,
    DateTimeOffset Timestamp,
    string? Summary = null)
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
