using AgentSmith.Application.Services.Health;
using AgentSmith.Server.Services;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
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
        // The work says when it has been entered, and the test cancels then — so the claim is
        // that a connected subsystem runs its work, not that two hundred milliseconds were
        // enough for it to get there.
        var health = new SubsystemHealth("queue_consumer");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = BuildProviderWith<IFakeService>(new FakeService(), connected: true);
        using var cts = new CancellationTokenSource();

        var run = SubsystemTask.RunRedisGatedAsync<IFakeService>(
            provider, health, retryIntervalSeconds: 1,
            (_, ct) => { entered.TrySetResult(); return Task.Delay(Timeout.Infinite, ct); },
            NullLogger.Instance, cts.Token);

        await entered.Task.OrHang("the gated work is entered");
        await cts.CancelAsync();
        await run.OrHang("the gated loop returns once cancelled");

        health.State.Should().Be(SubsystemState.Up);
    }

    [Fact]
    public async Task RunRedisGatedAsync_DisconnectedThenCancelled_RemainsDegraded()
    {
        var health = new SubsystemHealth("queue_consumer");
        var provider = BuildProviderWith<IFakeService>(new FakeService(), connected: false);
        using var cts = new CancellationTokenSource();

        var run = SubsystemTask.RunRedisGatedAsync<IFakeService>(
            provider, health, retryIntervalSeconds: 1,
            (_, _) => Task.CompletedTask,
            NullLogger.Instance, cts.Token);
        await TestWaits.UntilAsync(
            () => health.State == SubsystemState.Degraded, "the subsystem reports it is waiting");
        await cts.CancelAsync();
        await run.OrHang("the retry loop returns once cancelled");

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

    public interface IFakeService { }
    public sealed class FakeService : IFakeService { }
}
