namespace AgentSmith.Contracts.Events;

/// <summary>
/// Base type for all events published into <c>run:{runId}:events</c>. The
/// <see cref="Type"/> discriminator drives JSON polymorphic serialisation on
/// the broadcaster + dashboard sides.
/// </summary>
public abstract record RunEvent(string RunId, EventType Type, DateTimeOffset Timestamp);
