using AgentSmith.Contracts.Events;

namespace AgentSmith.Tests.Events;

/// <summary>
/// Records every published <see cref="RunEvent"/>. Tests assert on
/// <see cref="Events"/> by type, or use <see cref="Types"/> for set
/// membership.
/// </summary>
public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<RunEvent> _events = new();
    private readonly object _lock = new();

    public IReadOnlyList<RunEvent> Events
    {
        get { lock (_lock) return _events.ToArray(); }
    }

    public IReadOnlySet<EventType> Types => Events.Select(e => e.Type).ToHashSet();

    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock) _events.Add(runEvent);
        return Task.CompletedTask;
    }
}
