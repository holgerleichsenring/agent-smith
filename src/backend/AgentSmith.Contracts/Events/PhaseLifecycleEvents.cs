using AgentSmith.Contracts.Specs;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0466: a phase of the sequence changed standing — selected, through, or stopped.
/// <para>
/// The phase is the unit the operator reasons in, and until now it existed only as a
/// string prefix on a step name. This event is the phase saying, in its own words, who
/// it is (<paramref name="Ordinal"/>, <paramref name="Title"/>) and where it stands, so
/// the server can hold a row for it instead of parsing one out of a display label.
/// </para>
/// <para>
/// The event stream is the only DB channel a spawned orchestrator has, which is why the
/// standing travels as an event rather than a direct write.
/// </para>
/// </summary>
/// <param name="Ordinal">The phase's 1-based position in the derived sequence.</param>
/// <param name="Verdict">Why the standing is what it is — the failing command of a
/// stopped phase, or the note explaining a phase that was through before it started.
/// Null while the phase is simply running.</param>
public sealed record PhaseStateChangedEvent(
    string RunId,
    string PhaseId,
    int Ordinal,
    string Title,
    PhaseRunState State,
    string? Verdict,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.PhaseStateChanged, Timestamp);

/// <summary>
/// p0466: the spec a phase actually executed, verbatim, as WritePhaseRecord wrote it
/// into the working tree. That copy reaches the pull request and nowhere else; this one
/// reaches the server, so a finished phase can be opened after the branch is merged and
/// the sandbox is gone.
/// </summary>
public sealed record PhaseRecordedEvent(
    string RunId,
    string PhaseId,
    string Body,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.PhaseRecorded, Timestamp);
