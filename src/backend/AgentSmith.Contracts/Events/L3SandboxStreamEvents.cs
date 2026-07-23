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
    string? Summary = null,
    // p0357: true when the command mutates the working tree — a WriteFile step OR a
    // RunCommand whose shell text the MutatingCommandClassifier flags (perl -i,
    // cat > f, git apply, …). The dashboard's write counter reads this instead of
    // guessing from the verb, so script edits no longer read as plain actions.
    bool IsWrite = false)
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

// Additive trailing optionals (0/null for events from older servers).
// p0367: OutputTail — a COMPACT truncated tail of the command's stdout/stderr,
// populated primarily on a non-zero exit so build/test failures are finally durable
// and inspectable (the per-line SandboxOutputEvent stream is never persisted).
// p0369: Summary mirrors the SandboxCommand one-liner (path for file ops, "-c <cmd>"
// for shell runs) so the run-metrics fold can classify build/test invocations without
// a second event; ContentHash is the SHA-256 of the content actually touched (READ
// content for ReadFile, WRITTEN content for WriteFile, null otherwise) so redundant
// re-reads/re-writes are detected on (path + content), not path alone.
public sealed record SandboxResultEvent(
    string RunId,
    string Repo,
    string Command,
    int ExitCode,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? OutputTail = null,
    string? Summary = null,
    string? ContentHash = null)
    : RunEvent(RunId, EventType.SandboxResult, Timestamp);
