using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: whether a held sandbox is still there is ONE Redis read of the key the
/// agent writes every two seconds and deletes on a clean exit. The alternative was pushing a
/// step at it and waiting out the step timeout plus a thirty-second grace.
/// </summary>
public sealed class SandboxHeartbeatProbeTests
{
    private const string JobId = "job-7";

    private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
    private readonly Mock<IDatabase> _database = new();

    public SandboxHeartbeatProbeTests() =>
        _multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);

    [Fact]
    public async Task Heartbeat_AKeyThatIsStillThere_SaysTheSandboxIsAlive()
    {
        Answering(present: true);

        (await Probe().IsAliveAsync(JobId, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Heartbeat_AKeyThatHasExpiredOrWasDeleted_SaysTheSandboxIsGone()
    {
        Answering(present: false);

        (await Probe().IsAliveAsync(JobId, CancellationToken.None)).Should().BeFalse(
            "the agent deletes the key on a clean exit, so a missing key is gone either way");
    }

    [Fact]
    public async Task Heartbeat_ARedisThatCannotBeRead_AnswersNo()
    {
        _database.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        (await Probe().IsAliveAsync(JobId, CancellationToken.None)).Should().BeFalse(
            "spawning is slow and correct; reading through a corpse is fast and wrong");
    }

    [Fact]
    public async Task Heartbeat_WithNoRedisAtAll_NeverHandsAHoldBack()
    {
        (await new NoSandboxHeartbeatProbe().IsAliveAsync(JobId, CancellationToken.None))
            .Should().BeFalse("the default composition verifies nothing, so it holds nothing");
    }

    private void Answering(bool present) =>
        _database.Setup(d => d.KeyExistsAsync(
                It.Is<RedisKey>(k => (string)k! == RedisKeys.HeartbeatKey(JobId)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(present);

    private RedisSandboxHeartbeatProbe Probe() =>
        new(_multiplexer.Object, NullLogger<RedisSandboxHeartbeatProbe>.Instance);
}
