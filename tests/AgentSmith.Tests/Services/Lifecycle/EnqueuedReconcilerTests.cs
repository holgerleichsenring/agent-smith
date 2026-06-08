using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
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

public sealed class EnqueuedReconcilerTests
{
    [Fact]
    public async Task EnqueuedWithoutFreshLease_ReEnqueues()
    {
        // p0252: no fresh lease (default Moq → GetByTicketAsync returns null) means
        // the run never started / died — re-enqueue. Liveness is the lease now.
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.ProjectName == "proj"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnqueuedWithFreshLease_NoReEnqueue()
    {
        // p0252: a fresh lease means a claim/run is already in flight — don't re-enqueue.
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        harness.Lease.Setup(l => l.GetByTicketAsync(
            "proj", It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("proj", new TicketId("42"), "run-1", null, DateTimeOffset.UtcNow));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Harness
    {
        public Mock<IActiveRunLease> Lease { get; } = new();
        public Mock<IRedisJobQueue> JobQueue { get; } = new();
        public Mock<ITicketProviderFactory> TicketFactory { get; } = new();
        public Mock<ITicketProvider> Provider { get; } = new();
        public Mock<IConfigurationLoader> ConfigLoader { get; } = new();

        public Harness()
        {
            TicketFactory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(Provider.Object);
            ConfigLoader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
            {
                Projects = new() { ["proj"] = new ResolvedProject { Pipeline = "fix-bug" } }
            });
        }

        public void SetupEnqueuedTicket(string id)
            => Provider.Setup(p => p.ListByLifecycleStatusAsync(
                TicketLifecycleStatus.Enqueued, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket(new TicketId(id), "t", "d", null, "open", "GitHub")]);

        public EnqueuedReconciler BuildSut() => new(
            Lease.Object, JobQueue.Object, TicketFactory.Object,
            ConfigLoader.Object, new PipelineConfigResolver(), TimeProvider.System, "config.yml",
            NullLogger<EnqueuedReconciler>.Instance);
    }
}
