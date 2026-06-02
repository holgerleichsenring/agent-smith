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

        await fixture.RunForAsync(ticks: SandboxLivenessWatcher.MissThreshold + 2);

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

        await fixture.RunForAsync(ticks: SandboxLivenessWatcher.MissThreshold + 2);

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
        }

        public async Task RunForAsync(int ticks)
        {
            var watcher = new SandboxLivenessWatcher(
                Multiplexer.Object, Docker.Object, Registry.Object, Publisher.Object,
                new SandboxLivenessTarget(RunId, JobId, ContainerId, SandboxKey),
                NullLogger<SandboxLivenessWatcher>.Instance);
            watcher.Start();
            // Add slack to the polling cadence so the loop actually completes the
            // expected number of ticks under test-host scheduling jitter.
            var wait = SandboxLivenessWatcher.PollInterval.TotalMilliseconds * ticks + 500;
            await Task.Delay(TimeSpan.FromMilliseconds(wait));
            await watcher.DisposeAsync();
        }
    }
}
