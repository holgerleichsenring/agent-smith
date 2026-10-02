using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-02-5ab2f: dispose deletes the job keys, but a crashed server never disposes. Every
/// push refreshes a 24 h expiry on all three, so a dead job's keys do not live forever.
/// </summary>
public sealed class SandboxRedisChannelExpiryTests
{
    [Fact]
    public async Task SandboxRedisChannel_PushStep_ExpiresAllThreeJobKeys()
    {
        var expired = new List<(string Key, TimeSpan? Ttl)>();
        var db = new Mock<IDatabase>();
        db.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, TimeSpan?, ExpireWhen, CommandFlags>((key, ttl, _, _) => expired.Add(((string)key!, ttl)))
            .ReturnsAsync(true);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var channel = new SandboxRedisChannel(multiplexer.Object, "job-abc",
            NullLogger<SandboxRedisChannel>.Instance, Mock.Of<IWireProtocolWatcher>());

        await channel.PushStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, "echo", ["x"], "/", null, 10),
            CancellationToken.None);

        expired.Select(e => e.Key).Should().BeEquivalentTo(
            RedisKeys.InputKey("job-abc"), RedisKeys.ResultsKey("job-abc"), RedisKeys.EventsKey("job-abc"));
        expired.Should().OnlyContain(e => e.Ttl == RedisKeys.JobKeyTtl);
    }
}
