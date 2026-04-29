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
    public async Task EnqueuedWithoutHeartbeat_ReEnqueues()
    {
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        harness.Heartbeat.Setup(h => h.IsAliveAsync(
            It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.Is<PipelineRequest>(r => r.ProjectName == "proj"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnqueuedWithFreshHeartbeat_NoReEnqueue()
    {
        var harness = new Harness();
        harness.SetupEnqueuedTicket("42");
        harness.Heartbeat.Setup(h => h.IsAliveAsync(
            It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        await harness.BuildSut().RunAsync(cts.Token);

        harness.JobQueue.Verify(q => q.EnqueueAsync(
            It.IsAny<PipelineRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Harness
    {
        public Mock<IJobHeartbeatService> Heartbeat { get; } = new();
        public Mock<IRedisJobQueue> JobQueue { get; } = new();
        public Mock<ITicketProviderFactory> TicketFactory { get; } = new();
        public Mock<ITicketProvider> Provider { get; } = new();
        public Mock<IConfigurationLoader> ConfigLoader { get; } = new();

        public Harness()
        {
            TicketFactory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(Provider.Object);
            ConfigLoader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
            {
                Projects = new() { ["proj"] = new ProjectConfig { Pipeline = "fix-bug" } }
            });
        }

        public void SetupEnqueuedTicket(string id)
            => Provider.Setup(p => p.ListByLifecycleStatusAsync(
                TicketLifecycleStatus.Enqueued, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Ticket(new TicketId(id), "t", "d", null, "open", "GitHub")]);

        public EnqueuedReconciler BuildSut() => new(
            Heartbeat.Object, JobQueue.Object, TicketFactory.Object,
            ConfigLoader.Object, new PipelineConfigResolver(), "config.yml",
            NullLogger<EnqueuedReconciler>.Instance);
    }
}
