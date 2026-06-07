using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

public sealed class StaleJobDetectorTests
{
    [Fact]
    public async Task InProgressWithoutHeartbeat_RevertsToPending()
    {
        var harness = new Harness();
        harness.SetupInProgressTicket("42");
        harness.Heartbeat.Setup(h => h.IsAliveAsync(
            It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            TicketLifecycleStatus.InProgress,
            TicketLifecycleStatus.Pending,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task InProgressWithFreshHeartbeat_NoRevert()
    {
        var harness = new Harness();
        harness.SetupInProgressTicket("42");
        harness.Heartbeat.Setup(h => h.IsAliveAsync(
            It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.Transitioner.Verify(t => t.TransitionAsync(
            It.IsAny<TicketId>(),
            It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<TicketLifecycleStatus>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevertStale_CancelsTheActiveRun_BeforeRevert()
    {
        // p0242: never revert-without-cancel. A stale revert must authoritatively
        // cancel the run it reverts (registry + cross-process event) so an old run
        // can't survive next to a fresh spawn.
        var harness = new Harness();
        harness.SetupInProgressTicket("42");
        harness.Heartbeat.Setup(h => h.IsAliveAsync(
            It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        harness.Lease.Setup(l => l.GetByTicketAsync(
            "proj", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("proj", new TicketId("42"), "run-99", JobId: null));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.Cancellation.Verify(c => c.TryCancel("run-99", "stale-revert"), Times.AtLeastOnce);
        harness.Events.Verify(e => e.PublishAsync(
            It.Is<RunCancelRequestedEvent>(ev => ev.RunId == "run-99"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private sealed class Harness
    {
        public Mock<IJobHeartbeatService> Heartbeat { get; } = new();
        public Mock<ITicketProviderFactory> TicketFactory { get; } = new();
        public Mock<ITicketStatusTransitionerFactory> TransitionerFactory { get; } = new();
        public Mock<ITicketProvider> Provider { get; } = new();
        public Mock<ITicketStatusTransitioner> Transitioner { get; } = new();
        public Mock<IConfigurationLoader> ConfigLoader { get; } = new();
        public Mock<IActiveRunLease> Lease { get; } = new();
        public Mock<IRunCancellationRegistry> Cancellation { get; } = new();
        public Mock<IEventPublisher> Events { get; } = new();

        public Harness()
        {
            TicketFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Provider.Object);
            TransitionerFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Transitioner.Object);
            Transitioner.Setup(t => t.TransitionAsync(
                It.IsAny<TicketId>(),
                It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<TicketLifecycleStatus>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TransitionResult.Succeeded());
            ConfigLoader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
            {
                Projects = new() { ["proj"] = new ResolvedProject() }
            });
        }

        public void SetupInProgressTicket(string id)
            => Provider.Setup(p => p.ListByLifecycleStatusAsync(
                TicketLifecycleStatus.InProgress, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket(new TicketId(id), "t", "d", null, "open", "GitHub")]);

        public StaleJobDetector BuildSut() => new(
            Heartbeat.Object, TicketFactory.Object, TransitionerFactory.Object,
            Lease.Object, Cancellation.Object, Events.Object, TimeProvider.System,
            ConfigLoader.Object, "config.yml",
            NullLogger<StaleJobDetector>.Instance);
    }
}
