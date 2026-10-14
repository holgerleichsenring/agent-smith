using AgentSmith.Contracts.Specs;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0466: a spec of the sequence changed standing — selected, through, or stopped.
/// <para>
/// The spec is the unit the operator reasons in, and until now it existed only as a
/// string prefix on a step name. This event is the spec saying, in its own words, who
/// it is (<paramref name="Ordinal"/>, <paramref name="Title"/>) and where it stands, so
/// the server can hold a row for it instead of parsing one out of a display label.
/// </para>
/// <para>
/// The event stream is the only DB channel a spawned orchestrator has, which is why the
/// standing travels as an event rather than a direct write.
/// </para>
/// </summary>
/// <param name="Ordinal">The spec's 1-based position in the derived sequence.</param>
/// <param name="Verdict">Why the standing is what it is — the failing command of a
/// stopped spec, or the note explaining a spec that was through before it started.
/// Null while the spec is simply running.</param>
/// <param name="AcceptanceJson">2026-10-13-9a59e: the spec's own acceptance when the event was
/// written — its accounts and declined criteria rendered as the run detail renders them; null when the
/// spec has none yet.</param>
public sealed record SpecStateChangedEvent(
    string RunId,
    string SpecId,
    int Ordinal,
    string Title,
    SpecRunState State,
    string? Verdict,
    DateTimeOffset Timestamp,
    string? AcceptanceJson = null)
    : RunEvent(RunId, EventType.SpecStateChanged, Timestamp);

/// <summary>
/// p0466: the spec a spec actually executed, verbatim, as WriteSpecRecord wrote it
/// into the working tree. That copy reaches the pull request and nowhere else; this one
/// reaches the server, so a finished spec can be opened after the branch is merged and
/// the sandbox is gone.
/// </summary>
public sealed record SpecRecordedEvent(
    string RunId,
    string SpecId,
    string Body,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SpecRecorded, Timestamp);

/// <summary>
/// 2026-09-17-042eh: what a fresh instance reported against the spec's own diff, as JSON.
/// The record body states it in prose for whoever opens the spec; this row is what a reader
/// asks for it BY SPEC, without parsing a markdown body back apart.
/// </summary>
/// <param name="ReportJson">A serialised SpecReviewReport. An OBJECT, not a bare array:
/// `reviewed` false says nobody was asked and `why` names which reason it was.</param>
public sealed record SpecReviewedEvent(
    string RunId,
    string SpecId,
    string ReportJson,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SpecReviewed, Timestamp);
