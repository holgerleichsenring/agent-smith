using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Server.Hosting;

/// <summary>
/// 2026-11-22-747db: a worker's StartAsync returns at once and the worker is built and started only once the
/// listener is up — whatever its constructor, StartAsync or first step costs.
/// </summary>
public sealed class WorkerLauncherTests
{
    [Fact]
    public async Task StartAsync_WorkerThatBlocksWhileBuilt_ReturnsWithoutBuildingIt()
    {
        using var lifetime = new TestLifetime();
        using var gate = new ManualResetEventSlim();
        var built = false;
        await using var provider = Provider(lifetime, s => s
            .AddSingleton(_ => { built = true; gate.Wait(TestWaits.Hang); return new RecordingWorker(); })
            .AddWorker<RecordingWorker>());
        var launcher = provider.GetServices<IHostedService>().Single();

        await launcher.StartAsync(CancellationToken.None).OrHang("a StartAsync that builds nothing");

        built.Should().BeFalse("nothing is built before ApplicationStarted");
        lifetime.Started();
        (await TestWaits.ReachedAsync(() => built)).Should().BeTrue("ApplicationStarted builds it, off the host's call");
        gate.Set();
        await launcher.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ApplicationStarted_StartsTheRegisteredSingleton_AndStopAsyncStopsIt()
    {
        using var lifetime = new TestLifetime();
        await using var provider = Provider(lifetime, s => s.AddSingleton<RecordingWorker>().AddWorker<RecordingWorker>());
        var launcher = (WorkerLauncher<RecordingWorker>)provider.GetServices<IHostedService>().Single();

        await launcher.StartAsync(CancellationToken.None);
        lifetime.Started();
        var started = await TestWaits.ReachedAsync(() => launcher.Worker is not null);
        await launcher.StopAsync(CancellationToken.None);

        started.Should().BeTrue();
        launcher.Worker.Should().BeSameAs(provider.GetRequiredService<RecordingWorker>(), "never a second copy");
        launcher.Worker!.Stopped.Should().BeTrue();
    }

    [Fact]
    public async Task Worker_ExecuteFaults_IsLogged()
    {
        using var lifetime = new TestLifetime();
        using var logs = new RecordingLoggerProvider();
        await using var provider = Provider(lifetime, s => s.AddWorker<FaultingWorker>(), logs);
        var launcher = provider.GetServices<IHostedService>().Single();

        await launcher.StartAsync(CancellationToken.None);
        lifetime.Started();
        var logged = await TestWaits.ReachedAsync(() =>
            logs.Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains("FaultingWorker", StringComparison.Ordinal)));
        await launcher.StopAsync(CancellationToken.None);

        logged.Should().BeTrue("behind a launcher the host no longer sees a dead loop");
    }

    [Fact]
    public async Task StopAsync_NeverStarted_DoesNothing()
    {
        using var lifetime = new TestLifetime();
        await using var provider = Provider(lifetime, s => s.AddWorker<RecordingWorker>());
        var launcher = provider.GetServices<IHostedService>().Single();

        await launcher.StartAsync(CancellationToken.None);
        var stop = () => launcher.StopAsync(CancellationToken.None);

        await stop.Should().NotThrowAsync();
    }

    private static ServiceProvider Provider(
        TestLifetime lifetime, Action<IServiceCollection> register, RecordingLoggerProvider? logs = null)
    {
        var services = new ServiceCollection()
            .AddLogging(b => { if (logs is not null) b.AddProvider(logs); })
            .AddSingleton<IHostApplicationLifetime>(lifetime);
        register(services);
        return services.BuildServiceProvider();
    }

    private sealed class TestLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void Started() => _started.Cancel();

        public void StopApplication() => _stopping.Cancel();

        public void Dispose()
        {
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }

    private sealed class RecordingWorker : IHostedService
    {
        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FaultingWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("the loop died");
        }
    }
}
