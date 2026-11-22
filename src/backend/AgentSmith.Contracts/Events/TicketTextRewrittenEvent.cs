namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-22-e812: the run rewrote its ticket's text in the tracker from the approved specs (2026-11-22-a720b) — the
/// text and its fingerprint as the tracker stored it, label note stripped, converted as a fetch converts it. The attempt
/// keeps it as the text it left behind, so the next attempt's fetch of the same text is not a person's edit.
/// </summary>
public sealed record TicketTextRewrittenEvent(
    string RunId,
    string TicketId,
    string Description,
    string TicketFingerprint,
    DateTimeOffset Timestamp,
    string? AcceptanceCriteria = null)
    : RunEvent(RunId, EventType.TicketTextRewritten, Timestamp);
