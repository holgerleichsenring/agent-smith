using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxRedisChannelDisposeTests
{
    [Fact]
    public async Task DisposeAsync_DeletesAllThreeJobKeys()
    {
        var captured = new List<RedisKey[]>();
        var db = new Mock<IDatabase>();
        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey[], CommandFlags>((keys, _) => captured.Add(keys))
            .ReturnsAsync(3);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var channel = new SandboxRedisChannel(multiplexer.Object, "job-abc", NullLogger<SandboxRedisChannel>.Instance);

        await channel.DisposeAsync();

        captured.Should().ContainSingle();
        var keys = captured[0].Select(k => (string)k!).ToList();
        keys.Should().BeEquivalentTo(
            RedisKeys.InputKey("job-abc"),
            RedisKeys.EventsKey("job-abc"),
            RedisKeys.ResultsKey("job-abc"));
    }

    [Fact]
    public async Task DisposeAsync_RedisDeleteThrows_LogsAndReturnsWithoutThrowing()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var channel = new SandboxRedisChannel(multiplexer.Object, "job-abc", NullLogger<SandboxRedisChannel>.Instance);

        var act = async () => await channel.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
