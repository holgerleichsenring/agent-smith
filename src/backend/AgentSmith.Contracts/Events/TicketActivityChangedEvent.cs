namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-9a59a: something a ticket page shows changed for this ticket, so the dashboards
/// watching it refetch. A UI refresh signal carried on the system stream because every replica
/// drains that stream with its own cursor — the run-list nudge reaches only the sender's replica.
/// It never reaches the System page: SystemEventStream routes it to the overview group instead.
/// </summary>
/// <param name="TicketKey">TicketKey.For(tracker type lowercased, ticket id).Value — the spelling
/// a bound conversation stores.</param>
public sealed record TicketActivityChangedEvent(
    string Source,
    string Project,
    string TicketKey,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.TicketActivityChanged, Timestamp);
