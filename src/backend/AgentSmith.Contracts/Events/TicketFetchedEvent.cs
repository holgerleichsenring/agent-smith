namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0184: ticket details fetched by FetchTicketHandler at the top of the
/// pipeline. Carries enough context for the runs-page card to show a
/// human-readable title (TicketTitle) and for the Fetch-ticket step body
/// in the execution tree to render id / title / state / description /
/// attachment count without a separate API call.
///
/// Description is the raw text from the tracker; downstream UI may truncate.
/// AttachmentCount is the size of the attachments slot at fetch time;
/// the attachment payloads themselves stay in pipeline context, not on the
/// event (would bloat every run-detail page load with arbitrary blobs).
/// </summary>
public sealed record TicketFetchedEvent(
    string RunId,
    string TicketId,
    string Title,
    string Description,
    string State,
    IReadOnlyList<string> Labels,
    int AttachmentCount,
    string Source,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.TicketFetched, Timestamp);
