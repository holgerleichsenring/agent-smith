namespace AgentSmith.Contracts.Events;

/// <summary>
/// Base type for all events published into <c>run:{runId}:events</c>. The
/// <see cref="Type"/> discriminator drives JSON polymorphic serialisation on
/// the broadcaster + dashboard sides.
///
/// <para>p0173e: implements <see cref="IDomainEvent"/> via explicit interface
/// members derived from existing record fields. <c>EventId</c> is a
/// per-instance Guid captured at construction; the remaining members map to
/// <see cref="Timestamp"/> and <see cref="RunId"/> so the envelope shape stays
/// unchanged (frozen JSON fixtures remain compatible).</para>
/// </summary>
public abstract record RunEvent(string RunId, EventType Type, DateTimeOffset Timestamp) : IDomainEvent
{
    private readonly string _eventId = Guid.NewGuid().ToString();

    string IDomainEvent.EventId => _eventId;
    DateTimeOffset IDomainEvent.OccurredAt => Timestamp;
    string IDomainEvent.Origin => $"run:{RunId}";
    string? IDomainEvent.ParentEventId => null;
}
