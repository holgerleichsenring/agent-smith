using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0378: the Redis seam for JobsBroadcaster-level tests — an
/// <see cref="IConnectionMultiplexer"/> whose database is backed by
/// <see cref="FakeRedisState"/>, so the real RedisEventPublisher and the real
/// broadcaster drain run against the same in-memory streams without a live
/// Redis. Only the calls the publisher + broadcaster make are wired.
/// </summary>
public sealed class FakeRedisStreams
{
    public FakeRedisStreams()
    {
        var db = new Mock<IDatabase>();
        SetupStreams(db);
        SetupSets(db);
        SetupLists(db);
        SetupStrings(db);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(db.Object);
        Connection = redis.Object;
    }

    public FakeRedisState State { get; } = new();

    public IConnectionMultiplexer Connection { get; }

    private void SetupStreams(Mock<IDatabase> db)
    {
        db.Setup(d => d.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(),
                It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, NameValueEntry[] values, RedisValue? _, int? _, bool _, CommandFlags _) =>
                Task.FromResult(State.StreamAdd(key.ToString(), values)));
        db.Setup(d => d.StreamReadAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<int?>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue position, int? count, CommandFlags _) =>
                Task.FromResult(State.StreamReadAfter(key.ToString(), position, count)));
        db.Setup(d => d.StreamRangeAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue?>(),
                It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<Order>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue? _, RedisValue? _, int? count, Order order, CommandFlags _) =>
                Task.FromResult(State.StreamRange(key.ToString(), count, order)));
        db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) => Task.FromResult(State.KeyExists(key.ToString())));
    }

    // 2026-08-24-ca23: the drain's stored position. Without these the mock returns default
    // for both — a null read and a swallowed write — so the drain would silently fall back to
    // reading from the stream's beginning and every test would stay green over the defect.
    private void SetupStrings(Mock<IDatabase> db)
    {
        db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? _, bool _, When _, CommandFlags _) =>
            {
                State.StringSet(key.ToString(), value.ToString());
                return Task.FromResult(true);
            });
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) => Task.FromResult(State.StringGet(key.ToString())));
        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) => Task.FromResult(State.KeyDelete(key.ToString())));
    }

    private void SetupSets(Mock<IDatabase> db)
    {
        db.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue member, CommandFlags _) =>
            {
                State.SetAdd(key.ToString(), member.ToString());
                return Task.FromResult(true);
            });
        db.Setup(d => d.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue member, CommandFlags _) =>
            {
                State.SetRemove(key.ToString(), member.ToString());
                return Task.FromResult(true);
            });
        db.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) => Task.FromResult(State.SetMembers(key.ToString())));
    }

    private void SetupLists(Mock<IDatabase> db)
    {
        db.Setup(d => d.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, When _, CommandFlags _) =>
            {
                State.ListLeftPush(key.ToString(), value.ToString());
                return Task.FromResult(1L);
            });
        db.Setup(d => d.ListRangeAsync(It.IsAny<RedisKey>(), It.IsAny<long>(),
                It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, long _, long _, CommandFlags _) =>
                Task.FromResult(State.ListRange(key.ToString())));
        db.Setup(d => d.ListRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, long _, CommandFlags _) =>
            {
                State.ListRemove(key.ToString(), value.ToString());
                return Task.FromResult(1L);
            });
    }
}
