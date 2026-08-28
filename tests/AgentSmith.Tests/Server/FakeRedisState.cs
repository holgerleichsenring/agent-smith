using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0378: in-memory Redis backing state for the broadcaster drain tests —
/// streams with monotonic ids plus the two pointer indices (active SET,
/// recent LIST), faithfully modelling XADD/XREAD/XRANGE/SADD/SREM/LPUSH
/// semantics. Thread-safe: the broadcaster loop reads concurrently with
/// the test publishing.
/// </summary>
public sealed class FakeRedisState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<StreamEntry>> _streams = new();
    private readonly Dictionary<string, HashSet<string>> _sets = new();
    private readonly Dictionary<string, List<string>> _lists = new();
    private long _nextId;

    public RedisValue StreamAdd(string key, NameValueEntry[] values)
    {
        lock (_gate)
        {
            var id = $"{++_nextId}-0";
            Stream(key).Add(new StreamEntry(id, values));
            return id;
        }
    }

    public StreamEntry[] StreamReadAfter(string key, RedisValue position, int? count)
    {
        lock (_gate)
        {
            var after = IdOf(position);
            var entries = Stream(key).Where(e => IdOf(e.Id) > after);
            return (count is null ? entries : entries.Take(count.Value)).ToArray();
        }
    }

    public StreamEntry[] StreamRange(string key, int? count, Order order)
    {
        lock (_gate)
        {
            var entries = order == Order.Descending
                ? Stream(key).AsEnumerable().Reverse() : Stream(key);
            return (count is null ? entries : entries.Take(count.Value)).ToArray();
        }
    }

    public bool KeyExists(string key)
    {
        lock (_gate) return _streams.ContainsKey(key);
    }

    public void SetAdd(string key, string member)
    {
        lock (_gate) Collection(_sets, key, () => new HashSet<string>()).Add(member);
    }

    public void SetRemove(string key, string member)
    {
        lock (_gate) Collection(_sets, key, () => new HashSet<string>()).Remove(member);
    }

    public RedisValue[] SetMembers(string key)
    {
        lock (_gate) return Collection(_sets, key, () => new HashSet<string>())
            .Select(m => (RedisValue)m).ToArray();
    }

    // 2026-08-24-ca23: the drain's stored position per run — a plain string key with a TTL
    // the fake ignores, since no test outlives it.
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public void StringSet(string key, string value)
    {
        lock (_gate) _strings[key] = value;
    }

    public RedisValue StringGet(string key)
    {
        lock (_gate) return _strings.TryGetValue(key, out var v) ? v : RedisValue.Null;
    }

    /// <summary>
    /// 2026-08-28-3793: the config epoch is a counter, and a mock that answered default
    /// for INCR let "the restore signalled the epoch" pass over a signal that never
    /// happened — the write guard swallows a failing bump on purpose.
    /// </summary>
    public long StringIncrement(string key)
    {
        lock (_gate)
        {
            var next = (_strings.TryGetValue(key, out var current) ? long.Parse(current) : 0L) + 1;
            _strings[key] = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return next;
        }
    }

    public bool KeyDelete(string key)
    {
        lock (_gate) return _strings.Remove(key);
    }

    public void ListLeftPush(string key, string value)
    {
        lock (_gate) Collection(_lists, key, () => new List<string>()).Insert(0, value);
    }

    public RedisValue[] ListRange(string key)
    {
        lock (_gate) return Collection(_lists, key, () => new List<string>())
            .Select(m => (RedisValue)m).ToArray();
    }

    public void ListRemove(string key, string value)
    {
        lock (_gate) Collection(_lists, key, () => new List<string>()).Remove(value);
    }

    private List<StreamEntry> Stream(string key) =>
        Collection(_streams, key, () => new List<StreamEntry>());

    private static T Collection<T>(Dictionary<string, T> store, string key, Func<T> create)
    {
        if (!store.TryGetValue(key, out var value)) store[key] = value = create();
        return value;
    }

    private static long IdOf(RedisValue id) =>
        long.Parse(id.ToString().Split('-')[0]);
}
