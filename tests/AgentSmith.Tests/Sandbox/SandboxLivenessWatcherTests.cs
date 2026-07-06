using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
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

        fixture.Registry.Verify(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

        fixture.Registry.Verify(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "Redis-hiccup-but-container-Running must never cancel a live run");
        fixture.Publisher.Verify(p => p.PublishAsync(It.IsAny<SandboxVanishedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SandboxLivenessWatcher_HeartbeatMissing_ContainerProbeGone_SignalsCancelWithReasonSandboxVanished()
    {
        var fixture = new WatcherFixture();
        fixture.HeartbeatPresent = false;
        fixture.ProbeThrowsNotFound = true;

        // Poll-until-signal, not a fixed delay: the watcher's detect→cancel cadence is
        // real-time, so a fixed wait flaked under CI load. Complete as soon as both the
        // cancel and the vanish-event fire; fail only if neither happens within the window.
        await fixture.RunUntilAsync(fixture.CancelAndVanishObserved);

        fixture.Registry.Verify(r => r.TryCancel(RunId, SandboxLivenessWatcher.CancelReason), Times.AtLeastOnce);
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

        await fixture.RunUntilAsync(fixture.VanishObserved);

        fixture.Publisher.Verify(p => p.PublishAsync(
            It.Is<SandboxVanishedEvent>(e => e.ContainerState.Contains("137")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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

        public WatcherFixture()
        {
            Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Database.Object);
            Database.Setup(d => d.KeyExistsAsync(
                    It.Is<RedisKey>(k => (string)k! == RedisKeys.HeartbeatKey(JobId)),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(() => HeartbeatPresent);
            Docker.Setup(d => d.Containers).Returns(Containers.Object);
            Containers.Setup(c => c.InspectContainerAsync(ContainerId, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (ProbeThrowsNotFound) throw new DockerContainerNotFoundException(
                        System.Net.HttpStatusCode.NotFound, "container not found");
                    return Task.FromResult(new ContainerInspectResponse { State = ContainerState });
                });
            Registry.Setup(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => _cancelObserved.TrySetResult());
            Publisher.Setup(p => p.PublishAsync(It.IsAny<SandboxVanishedEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => _vanishObserved.TrySetResult())
                .Returns(Task.CompletedTask);
        }

        // "never cancels" tests: wait a bounded real-time window, then assert nothing fired.
        public async Task RunForAsync(int ticks)
        {
            var watcher = NewWatcher();
            watcher.Start();
            var wait = SandboxLivenessWatcher.PollInterval.TotalMilliseconds * ticks + 500;
            await Task.Delay(TimeSpan.FromMilliseconds(wait));
            await watcher.DisposeAsync();
        }

        // "signals" tests: run until the outcome fires (fast) or a generous timeout elapses
        // (only reached on a genuine failure — never on scheduling jitter).
        public async Task RunUntilAsync(Task signal)
        {
            var timeout = TimeSpan.FromMilliseconds(
                SandboxLivenessWatcher.PollInterval.TotalMilliseconds
                    * (SandboxLivenessWatcher.MissThreshold + 2) * 6 + 5000);
            var watcher = NewWatcher();
            watcher.Start();
            await Task.WhenAny(signal, Task.Delay(timeout));
            await watcher.DisposeAsync();
        }

        private SandboxLivenessWatcher NewWatcher() => new(
            Multiplexer.Object, Docker.Object, Registry.Object, Publisher.Object,
            new SandboxLivenessTarget(RunId, JobId, ContainerId, SandboxKey),
            NullLogger<SandboxLivenessWatcher>.Instance);
    }
}
