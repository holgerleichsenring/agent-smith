namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173e: marker for every public event record under
/// <c>AgentSmith.Contracts/Events</c>. The reflection-asserted invariant in
/// <c>DomainEventCoverageTests</c> fails the build when a new record forgets
/// to opt in. The four read-only members are derived from the existing record
/// fields by the <c>RunEvent</c> + <c>SystemEvent</c> base records via
/// explicit interface implementation, so they do not change the on-wire JSON
/// envelope produced by <c>EventEnvelopeSerializer</c>.
///
/// <para><b>EventId</b> uniquely identifies one emission instance — stable
/// for the lifetime of the record (not regenerated across calls); used by
/// downstream consumers that need a causality handle. <b>OccurredAt</b> is
/// the producer-observed timestamp. <b>Origin</b> is a free-form producer
/// tag (<c>run:{runId}</c>, <c>tracker:jira/sample</c>, …). <b>ParentEventId</b>
/// is optional — set when the event is a causal child of another emission.
/// </para>
/// </summary>
public interface IDomainEvent
{
    string EventId { get; }
    DateTimeOffset OccurredAt { get; }
    string Origin { get; }
    string? ParentEventId { get; }
}
