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
            registry.Object, publisher.Object, maxWallTimeSeconds: 60,
            NullLogger<PipelineRunWatchdog>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await watchdog.RunAsync(cts.Token);

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
        registry.Setup(r => r.TryCancel("run-overdue")).Returns(true);

        var watchdog = new PipelineRunWatchdog(
            registry.Object, publisher.Object, maxWallTimeSeconds: 60,
            NullLogger<PipelineRunWatchdog>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await watchdog.RunAsync(cts.Token);

        registry.Verify(r => r.TryCancel("run-overdue"), Times.AtLeastOnce);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<RunCancelRequestedEvent>(e => e.RunId == "run-overdue" && e.Reason == "watchdog"),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
