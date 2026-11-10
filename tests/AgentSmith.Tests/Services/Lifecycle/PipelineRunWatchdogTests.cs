using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class PipelineRunWatchdogTests
{
    [Fact]
    public async Task PipelineRunWatchdog_RunUnderThreshold_NoCancel()
    {
        var registry = new Mock<IRunCancellationRegistry>();
        var publisher = new Mock<IEventPublisher>();
        registry.Setup(r => r.Snapshot()).Returns(new[]
        {
            new RunCancellationEntry("run-1", DateTimeOffset.UtcNow.AddSeconds(-10)),
        });

        var watchdog = new PipelineRunWatchdog(
            registry.Object, publisher.Object, maxWallTimeSeconds: () => 60,
            NullLogger<PipelineRunWatchdog>.Instance);

        // p0254: drive one deterministic scan, not the timing-dependent RunAsync loop.
        await watchdog.ScanOnceAsync(CancellationToken.None);

        registry.Verify(r => r.TryCancel(It.IsAny<string>()), Times.Never);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PipelineRunWatchdog_RunOverThreshold_CallsTryCancel()
    {
        var registry = new Mock<IRunCancellationRegistry>();
        var publisher = new Mock<IEventPublisher>();
        registry.Setup(r => r.Snapshot()).Returns(new[]
        {
            new RunCancellationEntry("run-overdue", DateTimeOffset.UtcNow.AddSeconds(-3600)),
        });
        registry.Setup(r => r.TryCancel("run-overdue", "watchdog-wall-time")).Returns(true);

        var watchdog = new PipelineRunWatchdog(
            registry.Object, publisher.Object, maxWallTimeSeconds: () => 60,
            NullLogger<PipelineRunWatchdog>.Instance);

        // p0254: drive one deterministic scan, not the timing-dependent RunAsync loop.
        await watchdog.ScanOnceAsync(CancellationToken.None);

        registry.Verify(r => r.TryCancel("run-overdue", "watchdog-wall-time"), Times.AtLeastOnce);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<RunCancelRequestedEvent>(e => e.RunId == "run-overdue" && e.Reason == "watchdog"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PipelineRunWatchdog_DesignTurnOlderThanTheCeiling_IsNotCancelled()
    {
        var registry = new Mock<IRunCancellationRegistry>();
        var publisher = new Mock<IEventPublisher>();
        registry.Setup(r => r.Snapshot()).Returns(new[]
        {
            new RunCancellationEntry("turn", DateTimeOffset.UtcNow.AddSeconds(-3600), RegisteredRunKind.DesignTurn),
        });

        await NewWatchdog(registry, publisher).ScanOnceAsync(CancellationToken.None);

        registry.Verify(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PipelineRunWatchdog_CodingRunOlderThanTheCeiling_IsStillCancelled()
    {
        var registry = new Mock<IRunCancellationRegistry>();
        var publisher = new Mock<IEventPublisher>();
        registry.Setup(r => r.Snapshot()).Returns(new[]
        {
            new RunCancellationEntry("turn", DateTimeOffset.UtcNow.AddSeconds(-3600), RegisteredRunKind.DesignTurn),
            new RunCancellationEntry("coding", DateTimeOffset.UtcNow.AddSeconds(-3600), RegisteredRunKind.Pipeline),
        });
        registry.Setup(r => r.TryCancel("coding", "watchdog-wall-time")).Returns(true);

        await NewWatchdog(registry, publisher).ScanOnceAsync(CancellationToken.None);

        registry.Verify(r => r.TryCancel("coding", "watchdog-wall-time"), Times.Once);
        registry.Verify(r => r.TryCancel("turn", It.IsAny<string>()), Times.Never);
    }

    private static PipelineRunWatchdog NewWatchdog(
        Mock<IRunCancellationRegistry> registry, Mock<IEventPublisher> publisher) =>
        new(registry.Object, publisher.Object, maxWallTimeSeconds: () => 60,
            NullLogger<PipelineRunWatchdog>.Instance);
}
