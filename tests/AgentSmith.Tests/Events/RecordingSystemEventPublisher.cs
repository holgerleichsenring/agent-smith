using AgentSmith.Contracts.Events;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173a: records every published <see cref="SystemEvent"/>. Tests assert
/// on <see cref="Events"/> by type, or use <see cref="Types"/> for set
/// membership. Mirror of <see cref="RecordingEventPublisher"/> for the
/// system channel.
/// </summary>
public sealed class RecordingSystemEventPublisher : ISystemEventPublisher
{
    private readonly List<SystemEvent> _events = new();
    private readonly object _lock = new();

    public IReadOnlyList<SystemEvent> Events
    {
        get { lock (_lock) return _events.ToArray(); }
    }

    public IReadOnlySet<SystemEventType> Types => Events.Select(e => e.Type).ToHashSet();

    public Task PublishAsync(SystemEvent systemEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock) _events.Add(systemEvent);
        return Task.CompletedTask;
    }
}
