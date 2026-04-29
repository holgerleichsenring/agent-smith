using AgentSmith.Server.Services;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Cli.Services;

public sealed class RedisConnectionHealthTests
{
    [Fact]
    public void Constructor_MultiplexerConnected_SetsStateUp()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.SetupGet(m => m.IsConnected).Returns(true);

        var sut = new RedisConnectionHealth(mux.Object, NullLogger<RedisConnectionHealth>.Instance);

        sut.Health.Name.Should().Be("redis");
        sut.Health.State.Should().Be(SubsystemState.Up);
    }

    [Fact]
    public void Constructor_MultiplexerNotConnected_SetsStateDegraded()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.SetupGet(m => m.IsConnected).Returns(false);

        var sut = new RedisConnectionHealth(mux.Object, NullLogger<RedisConnectionHealth>.Instance);

        sut.Health.State.Should().Be(SubsystemState.Degraded);
        sut.Health.Reason.Should().Be("connecting");
    }
}
