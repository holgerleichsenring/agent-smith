namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0374a: the per-entry transitions of ONE accepted update_progress call —
/// entry, from-state, to-state, cause, master pass. Never folded into the run
/// row: its ledger column is a SNAPSHOT that every flush overwrites and cannot
/// hold history. 2026-10-13-9a59f: projected into its own RunLedgerTransition
/// row with the spec and run-wide pass the publish path stamped on it, because
/// the trail is pruned and names neither.
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
