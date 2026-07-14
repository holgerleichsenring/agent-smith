namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0328: the ratification outcome of the run's expectation negotiation.
/// Travels the event stream (not a direct DB write) because the producer may
/// be a spawned orchestrator whose only DB channel is this stream (p0330
/// lesson); the server-side applier persists it as the RunExpectation row.
/// DraftJson/RatifiedJson are serialized <c>ExpectationDraft</c> payloads.
/// </summary>
public sealed record ExpectationRatifiedEvent(
    string RunId,
    string DraftJson,
    string RatifiedJson,
    string Outcome,
    string RatifiedBy,
    int EditDistance,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.ExpectationRatified, Timestamp);
