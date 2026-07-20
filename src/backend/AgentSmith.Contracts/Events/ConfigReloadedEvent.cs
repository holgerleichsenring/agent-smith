namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0353: the poller leader rebuilt its pollers (and the settings enforcers
/// re-read config) after the epoch advanced. Correlated to a
/// <see cref="ConfigChangedEvent"/> by <see cref="Epoch"/> — matched = the change
/// is live; pending-but-unmatched = not yet applied.
/// </summary>
public sealed record ConfigReloadedEvent(
    string Source,
    long Epoch,
    int TrackerCount,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.ConfigReloaded, Timestamp);
