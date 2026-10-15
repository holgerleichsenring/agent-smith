namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-14-dbceg: a ticket landed — published once, by the replica whose landing insert won. A
/// ticket fact with no run, so it is a system event and joins the System page's ring.
/// </summary>
/// <param name="TicketKey">TicketKey.For(tracker type lowercased, ticket id).Value.</param>
/// <param name="Kind">merged or by-hand.</param>
/// <param name="By">The person who marked it by hand; null for a merge.</param>
public sealed record TicketLandedEvent(
    string Source,
    string Project,
    string TicketKey,
    string TicketId,
    string Kind,
    string? By,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.TicketLanded, Timestamp);
