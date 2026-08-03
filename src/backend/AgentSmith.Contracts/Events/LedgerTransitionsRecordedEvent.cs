namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0374a: the per-entry transitions of ONE accepted update_progress call —
/// entry, from-state, to-state, cause, master pass. Trail-only: the applier
/// persists the raw event row and touches no projection, because the run row's
/// ledger column is a SNAPSHOT that every flush overwrites and therefore cannot
/// hold history. The trail can, and it is the surface a watcher already reads.
/// Travels the event stream for the same reason the story snapshot does — a
/// spawned orchestrator has no other DB channel (p0330).
/// <para><see cref="TransitionsJson"/> is the camelCase wire JSON
/// (<c>RunStoryJson</c>) of an array of <c>LedgerTransitionView</c>.</para>
/// </summary>
public sealed record LedgerTransitionsRecordedEvent(
    string RunId,
    string TransitionsJson,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.LedgerTransitionsRecorded, Timestamp);
