using AgentSmith.Tests.Server;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// A message transport a booted test HAS, rather than one it points at and waits for.
/// <para>
/// A test does not need a Redis, and the rig used to say so by naming an address nothing
/// answers. Unreachable is not the same as absent: the multiplexer connects synchronously
/// while the hosted services are constructed, the startup probe then spends its whole
/// budget on the same endpoint, and every request whose handler touches the queue pays a
/// reconnect. Measured against the rig, that was ~26 seconds per boot.
/// </para>
/// <para>
/// This answers instead. The database is <see cref="FakeRedisStreams"/>' in-memory one, so
/// the streams, sets and lists a handler writes are really there to read back; everything
/// else answers empty. Nothing opens a socket.
/// </para>
/// </summary>
internal static class InMemoryRedis
{
    /// <summary>Reported where a real multiplexer would report its endpoint.</summary>
    internal const string Endpoint = "in-memory";

    internal static IConnectionMultiplexer Connection()
    {
        var database = new FakeRedisStreams().Connection.GetDatabase();
        Mock.Get(database)
            .Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.Zero);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database);
        redis.Setup(r => r.GetSubscriber(It.IsAny<object?>())).Returns(Subscriber());
        redis.Setup(r => r.IsConnected).Returns(true);
        redis.Setup(r => r.Configuration).Returns(Endpoint);
        redis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns([new System.Net.DnsEndPoint(Endpoint, 0)]);
        return redis.Object;
    }

    private static ISubscriber Subscriber()
    {
        var subscriber = new Mock<ISubscriber>();
        subscriber
            .Setup(s => s.SubscribeAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<Action<RedisChannel, RedisValue>>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);
        subscriber
            .Setup(s => s.PublishAsync(
                It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        return subscriber.Object;
    }
}
