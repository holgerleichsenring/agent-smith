using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-02-5f89c: Redis hashes held in memory and shared by every multiplexer made from one
/// instance — two replicas reading and writing one server. A transaction queues its commands and
/// applies them together on execute, as MULTI/EXEC does.
/// 2026-10-02-b540: <see cref="Down"/> stands for a Redis that cannot be reached — every command
/// throws the connection exception the client throws.
/// </summary>
internal sealed class FakeRedisHashes
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, RedisValue>> _hashes = new();

    public bool Down { get; set; }

    public IConnectionMultiplexer Replica()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(Database());
        return multiplexer.Object;
    }

    public void Flush(string key)
    {
        lock (_gate) _hashes.Remove(key);
    }

    private IDatabase Database()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.HashGet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, RedisValue f, CommandFlags _) => Get(k, f));
        db.Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey k, RedisValue f, CommandFlags _) => Get(k, f));
        db.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey k, CommandFlags _) => All(k));
        db.Setup(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, HashEntry[] e, CommandFlags _) => { Set(k, e); return Task.CompletedTask; });
        db.Setup(d => d.CreateTransaction(It.IsAny<object?>())).Returns(() => Transaction());
        return db.Object;
    }

    private ITransaction Transaction()
    {
        var queued = new List<Action>();
        var tx = new Mock<ITransaction>();
        tx.Setup(t => t.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, HashEntry[] e, CommandFlags _) => { queued.Add(() => Set(k, e)); return Task.CompletedTask; });
        tx.Setup(t => t.HashDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey k, RedisValue f, CommandFlags _) => { queued.Add(() => Delete(k, f)); return Task.FromResult(true); });
        tx.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(() =>
        {
            Reach();
            lock (_gate) queued.ForEach(apply => apply());
            return true;
        });
        return tx.Object;
    }

    private RedisValue Get(RedisKey key, RedisValue field)
    {
        Reach();
        lock (_gate)
            return _hashes.TryGetValue(key!, out var hash) && hash.TryGetValue(field!, out var v) ? v : RedisValue.Null;
    }

    private HashEntry[] All(RedisKey key)
    {
        Reach();
        lock (_gate)
            return _hashes.TryGetValue(key!, out var hash) ? [.. hash.Select(kv => new HashEntry(kv.Key, kv.Value))] : [];
    }

    private void Set(RedisKey key, HashEntry[] entries)
    {
        Reach();
        lock (_gate)
        {
            if (!_hashes.TryGetValue(key!, out var hash)) _hashes[key!] = hash = new Dictionary<string, RedisValue>();
            foreach (var entry in entries) hash[entry.Name!] = entry.Value;
        }
    }

    private void Delete(RedisKey key, RedisValue field)
    {
        lock (_gate)
            if (_hashes.TryGetValue(key!, out var hash)) hash.Remove(field!);
    }

    private void Reach()
    {
        if (Down) throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "No connection is available to service this operation.");
    }
}
