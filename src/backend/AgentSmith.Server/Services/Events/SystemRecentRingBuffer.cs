using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0173a: fixed-capacity FIFO buffer for the most recent system events.
/// Cold-starts from Redis XREVRANGE on the system:events stream, then
/// JobsBroadcaster's drain loop appends each new event. Default capacity
/// 500 — system events accumulate slower than run events, but the
/// operator wants a meaningful window for the dashboard KPI cards
/// (slice d). When at capacity, oldest event drops out.
/// </summary>
public sealed class SystemRecentRingBuffer(int capacity)
{
    private readonly LinkedList<SystemEvent> _ordered = new();
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _ordered.Count; }
    }

    public int Capacity { get; } = capacity;

    public IReadOnlyList<SystemEvent> Snapshot()
    {
        lock (_lock) return _ordered.ToArray();
    }

    public void Append(SystemEvent systemEvent)
    {
        lock (_lock)
        {
            _ordered.AddLast(systemEvent);
            while (_ordered.Count > capacity) _ordered.RemoveFirst();
        }
    }

    public void Prepend(SystemEvent systemEvent)
    {
        lock (_lock)
        {
            _ordered.AddFirst(systemEvent);
            while (_ordered.Count > capacity) _ordered.RemoveLast();
        }
    }
}
