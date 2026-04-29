using AgentSmith.Application.Services.Health;
using AgentSmith.Server.Services;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Cli.Services;

public sealed class SubsystemTaskTests
{
    [Fact]
    public async Task RunRedisGatedAsync_RedisHealthDisabled_SetsDisabledAndReturns()
    {
        var health = new SubsystemHealth("queue_consumer");
        var redis = new SubsystemHealth("redis");
        redis.SetDisabled("REDIS_URL not configured");
        var services = new ServiceCollection();
        services.AddSingleton<ISubsystemHealth>(redis);
        var provider = services.BuildServiceProvider();
        var workInvoked = false;

        await SubsystemTask.RunRedisGatedAsync<IFakeService>(
            provider, health, retryIntervalSeconds: 1,
            (_, _) => { workInvoked = true; return Task.CompletedTask; },
            NullLogger.Instance, CancellationToken.None);

        health.State.Should().Be(SubsystemState.Disabled);
        health.Reason.Should().Be("REDIS_URL not configured");
        workInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task RunRedisGatedAsync_ServiceRegisteredAndConnected_SetsUpAndRunsWork()
    {
        var health = new SubsystemHealth("queue_consumer");
        var workInvoked = false;
        var provider = BuildProviderWith<IFakeService>(new FakeService(), connected: true);

        var run = SubsystemTask.RunRedisGatedAsync<IFakeService>(
            provider, health, retryIntervalSeconds: 1,
            (_, ct) => { workInvoked = true; return Task.Delay(50, ct); },
            NullLogger.Instance, CancelAfter(200));

        await run;

        workInvoked.Should().BeTrue();
        health.State.Should().Be(SubsystemState.Up);
    }

    [Fact]
    public async Task RunRedisGatedAsync_DisconnectedThenCancelled_RemainsDegraded()
    {
        var health = new SubsystemHealth("queue_consumer");
        var provider = BuildProviderWith<IFakeService>(new FakeService(), connected: false);

        await SubsystemTask.RunRedisGatedAsync<IFakeService>(
            provider, health, retryIntervalSeconds: 1,
            (_, _) => Task.CompletedTask,
            NullLogger.Instance, CancelAfter(50));

        health.State.Should().Be(SubsystemState.Degraded);
        health.Reason.Should().Be("waiting for Redis");
    }

    private static IServiceProvider BuildProviderWith<TService>(TService impl, bool connected)
        where TService : class
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.SetupGet(m => m.IsConnected).Returns(connected);
        var services = new ServiceCollection();
        services.AddSingleton(impl);
        services.AddSingleton(mux.Object);
        return services.BuildServiceProvider();
    }

    private static CancellationToken CancelAfter(int ms)
    {
        var cts = new CancellationTokenSource(ms);
        return cts.Token;
    }

    public interface IFakeService { }
    public sealed class FakeService : IFakeService { }
}
