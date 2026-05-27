namespace AgentSmith.Server.Services.Events;

/// <summary>
/// Fixed-capacity LRU buffer for finished runs (default 50). Eviction is
/// by FinishedAt — oldest finished run drops out when at cap. Mirrors the
/// Redis <c>agentsmith:runs:recent</c> LTRIM bound so the in-memory and
/// on-disk views agree across restarts.
/// </summary>
public sealed class RecentRunsRingBuffer(int capacity)
{
    private readonly LinkedList<RunSnapshot> _ordered = new();
    private readonly Dictionary<string, LinkedListNode<RunSnapshot>> _index = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _ordered.Count; }
    }

    public IReadOnlyList<RunSnapshot> Snapshot()
    {
        lock (_lock) return _ordered.ToArray();
    }

    public void Upsert(RunSnapshot snapshot)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(snapshot.RunId, out var existing))
                _ordered.Remove(existing);
            var node = _ordered.AddFirst(snapshot);
            _index[snapshot.RunId] = node;
            while (_ordered.Count > capacity)
            {
                var last = _ordered.Last!;
                _index.Remove(last.Value.RunId);
                _ordered.RemoveLast();
            }
        }
    }

    public void Remove(string runId)
    {
        lock (_lock)
        {
            if (!_index.TryGetValue(runId, out var node)) return;
            _ordered.Remove(node);
            _index.Remove(runId);
        }
    }
}
