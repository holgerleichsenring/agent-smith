namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0344b: the run's story artifacts, snapshotted at run end by
/// WriteRunResultHandler — the p0341 progress ledger and the p0340 acceptance
/// dispositions paired with their ratified criteria. Travels the event stream
/// (not a direct DB write) because the producer may be a spawned orchestrator
/// whose only DB channel is this stream (p0330 lesson); the server-side applier
/// persists both payloads as JSON columns on the run row, which the run-detail
/// endpoint serves verbatim. Payloads are the camelCase wire JSON
/// (<c>RunStoryJson</c>): ProgressLedgerJson an array of
/// <c>ProgressLedgerItemView</c>, AcceptanceJson an <c>AcceptanceView</c>.
/// Either may be null — a run without a ledger / without a ratified contract
/// stores nothing and serves an honest null.
/// </summary>
public sealed record RunStoryRecordedEvent(
    string RunId,
    string? ProgressLedgerJson,
    string? AcceptanceJson,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.RunStoryRecorded, Timestamp);
