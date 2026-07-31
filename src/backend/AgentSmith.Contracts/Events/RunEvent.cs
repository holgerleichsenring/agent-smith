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

    /// <summary>
    /// p0388a: the pipeline step this event was PRODUCED IN, stamped on the
    /// publish path from the ambient step scope. Null outside any step and on
    /// pre-p0388a payloads, which read as unattributed — nothing infers a step
    /// for them. Named <c>OriginStepIndex</c> rather than <c>StepIndex</c>
    /// because <see cref="StepStartedEvent"/> / <see cref="StepFinishedEvent"/>
    /// / <see cref="L1StepDetailEvent"/> already carry a non-nullable
    /// <c>StepIndex</c> positional member of their own.
    /// </summary>
    public int? OriginStepIndex { get; init; }

    string IDomainEvent.EventId => _eventId;
    DateTimeOffset IDomainEvent.OccurredAt => Timestamp;
    string IDomainEvent.Origin => $"run:{RunId}";
    string? IDomainEvent.ParentEventId => null;
}
