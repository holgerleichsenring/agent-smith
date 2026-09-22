using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0201 fast-tier coverage for SandboxLivenessWatcher. The three table-stakes
/// branches are exercised with mocked Docker + Redis surfaces:
///   - heartbeat present → never cancels
///   - heartbeat missing AND container Running → never cancels (Redis hiccup)
///   - heartbeat missing AND container Gone → publishes SandboxVanishedEvent and
///     signals the registry with reason "sandbox-vanished"
/// </summary>
public sealed class SandboxLivenessWatcherTests
{
    private const string RunId = "run-1";
    private const string JobId = "job-1";
    private const string ContainerId = "container-abc";
    private const string SandboxKey = "primary/csharp";

    [Fact]
    public async Task SandboxLivenessWatcher_HeartbeatPresent_NoCancel()
    {
        var fixture = new WatcherFixture();
        fixture.HeartbeatPresent = true;

        await fixture.RunForAsync(ticks: SandboxLivenessWatcher.MissThreshold + 2);

        fixture.Registry.Verify(
            r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        fixture.Publisher.Verify(p => p.PublishAsync(It.IsAny<SandboxVanishedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SandboxLivenessWatcher_HeartbeatMissing_ContainerProbeRunning_NoCancel()
    {
        var fixture = new WatcherFixture();
        fixture.HeartbeatPresent = false;
        fixture.ContainerState = new ContainerState { Running = true };

        await fixture.RunForAsync(ticks: SandboxLivenessWatcher.MissThreshold + 2);

        fixture.Registry.Verify(
            r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never,
            "Redis-hiccup-but-container-Running must never cancel a live run");
        fixture.Publisher.Verify(p => p.PublishAsync(It.IsAny<SandboxVanishedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SandboxLiveness_AVanishedSandbox_IsNoticedWithoutADeadline()
    {
        var fixture = new WatcherFixture();
        fixture.HeartbeatPresent = false;
        fixture.ProbeThrowsNotFound = true;

        // 2026-09-22-3f7c: the signal was already the right thing to wait for; the window
        // behind it was still a number, and on a slow runner the number is what failed.
        // A cancel that never comes is a defect, so nothing but the suite's hang ceiling
        // bounds this wait.
        await fixture.RunUntilAsync(
            fixture.CancelAndVanishObserved, "the vanished sandbox is cancelled and announced");

        // p0396: a Gone container yields no inspect evidence — the detail stays
        // null so the summary falls back to a neutral sentence, not an OOM guess.
        fixture.Registry.Verify(
            r => r.TryCancel(RunId, SandboxLivenessWatcher.CancelReason, null), Times.AtLeastOnce);
        fixture.Publisher.Verify(p => p.PublishAsync(
            It.Is<SandboxVanishedEvent>(e =>
                e.RunId == RunId && e.JobId == JobId &&
                e.Reason == SandboxLivenessWatcher.CancelReason &&
                e.ContainerState == "Gone"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SandboxLivenessWatcher_HeartbeatMissing_ContainerExited_PublishesExitedState()
    {
        var fixture = new WatcherFixture();
        fixture.HeartbeatPresent = false;
        fixture.ContainerState = new ContainerState { Running = false, ExitCode = 137 };

        await fixture.RunUntilAsync(fixture.VanishObserved, "the exited container is announced");

        fixture.Publisher.Verify(p => p.PublishAsync(
            It.Is<SandboxVanishedEvent>(e => e.ContainerState.Contains("137")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        // p0396: inspect evidence exists — the exit-code-based detail travels
        // with the cancel so the run summary states the truth.
        fixture.Registry.Verify(r => r.TryCancel(
            RunId, SandboxLivenessWatcher.CancelReason,
            It.Is<string?>(d => d != null && d.Contains("exit code 137"))), Times.AtLeastOnce);
    }

    private sealed class WatcherFixture
    {
        public Mock<IConnectionMultiplexer> Multiplexer { get; } = new();
        public Mock<IDatabase> Database { get; } = new();
        public Mock<IDockerClient> Docker { get; } = new();
        public Mock<IContainerOperations> Containers { get; } = new();
        public Mock<IRunCancellationRegistry> Registry { get; } = new();
        public Mock<IEventPublisher> Publisher { get; } = new();
        public bool HeartbeatPresent { get; set; }
        public bool ProbeThrowsNotFound { get; set; }
        public ContainerState? ContainerState { get; set; }

        // Fired by Moq callbacks the moment the watcher acts, so the "signals" tests can
        // wait for the OUTCOME (deterministic) instead of a fixed real-time delay (flaky).
        private readonly TaskCompletionSource _cancelObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _vanishObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task VanishObserved => _vanishObserved.Task;
        public Task CancelAndVanishObserved => Task.WhenAll(_cancelObserved.Task, _vanishObserved.Task);

        // 2026-09-22-3f7c: one heartbeat probe is one watcher tick, so a "never cancels" test
        // can wait for the TICKS to have happened instead of for a stretch of clock in which
        // they might have. On a host that gives the loop no scheduling the old fixed window
        // asserted over a watcher that had barely run, and said nothing at all.
        private int _probes;
        public int Probes => Volatile.Read(ref _probes);

        public WatcherFixture()
        {
            Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Database.Object);
            Database.Setup(d => d.KeyExistsAsync(
                    It.Is<RedisKey>(k => (string)k! == RedisKeys.HeartbeatKey(JobId)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(() =>
                {
                    Interlocked.Increment(ref _probes);
                    return HeartbeatPresent;
                });
            Docker.Setup(d => d.Containers).Returns(Containers.Object);
            Containers.Setup(c => c.InspectContainerAsync(ContainerId, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (ProbeThrowsNotFound) throw new DockerContainerNotFoundException(
                        System.Net.HttpStatusCode.NotFound, "container not found");
                    return Task.FromResult(new ContainerInspectResponse { State = ContainerState });
                });
            Registry.Setup(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback(() => _cancelObserved.TrySetResult());
            Publisher.Setup(p => p.PublishAsync(It.IsAny<SandboxVanishedEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => _vanishObserved.TrySetResult())
                .Returns(Task.CompletedTask);
        }

        // "never cancels" tests: let the watcher tick a counted number of times, then assert
        // nothing fired. The count comes from the watcher's own probes, so the assertion holds
        // over the same number of decisions on every machine.
        public async Task RunForAsync(int ticks)
        {
            var watcher = NewWatcher();
            watcher.Start();
            try
            {
                await TestWaits.UntilAsync(() => Probes >= ticks, $"the watcher probes {ticks} times");
            }
            finally { await watcher.DisposeAsync(); }
        }

        // "signals" tests: run until the outcome fires. There is no second outcome to wait for
        // and no latency being claimed, so the only bound is the suite's hang ceiling.
        public async Task RunUntilAsync(Task signal, string awaited)
        {
            var watcher = NewWatcher();
            watcher.Start();
            try { await signal.OrHang(awaited); }
            finally { await watcher.DisposeAsync(); }
        }

        private SandboxLivenessWatcher NewWatcher() => new(
            Multiplexer.Object, Docker.Object, Registry.Object, Publisher.Object,
            new SandboxLivenessTarget(RunId, JobId, ContainerId, SandboxKey),
            NullLogger<SandboxLivenessWatcher>.Instance);
    }
}
