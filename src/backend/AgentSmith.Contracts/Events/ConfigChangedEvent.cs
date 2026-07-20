namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0353: a config write (Studio import / entity CRUD / settings save) bumped the
/// config epoch. Rendered as "config change N pending" until the leader publishes
/// the matching <see cref="ConfigReloadedEvent"/> for the same <see cref="Epoch"/>.
/// </summary>
public sealed record ConfigChangedEvent(
    string Source,
    long Epoch,
    string Actor,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.ConfigChanged, Timestamp);
