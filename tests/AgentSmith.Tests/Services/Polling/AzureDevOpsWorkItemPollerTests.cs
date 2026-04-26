using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Polling;

public sealed class AzureDevOpsWorkItemPollerTests
{
    [Fact]
    public async Task PollAsync_NoPendingTickets_ReturnsEmpty()
    {
        var sut = Build(pendingTickets: []);
        var result = await sut.PollAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PollAsync_WithPendingTickets_ReturnsClaimRequestsTaggedAzureDevOps()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("42"), "t1", "", null, "New", "AzureDevOps"),
            new Ticket(new TicketId("99"), "t2", "", null, "Active", "AzureDevOps")
        };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "fix-bug");

        var result = await sut.PollAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Platform.Should().Be("AzureDevOps");
        result[0].ProjectName.Should().Be("proj");
        result[0].PipelineName.Should().Be("fix-bug");
        result[0].TicketId.Value.Should().Be("42");
    }

    [Fact]
    public async Task PollAsync_NoTriggerConfig_FallsBackToFixBug()
    {
        var tickets = new[] { new Ticket(new TicketId("1"), "t", "", null, "New", "AzureDevOps") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: null);

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task PollAsync_AzuredevopsTriggerOverride_UsesConfiguredPipeline()
    {
        var tickets = new[] { new Ticket(new TicketId("1"), "t", "", null, "Active", "AzureDevOps") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "security-scan");

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("security-scan");
    }

    private static AzureDevOpsWorkItemPoller Build(
        Ticket[] pendingTickets, string? defaultPipeline = "fix-bug")
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingTickets);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(provider.Object);

        var project = new ProjectConfig();
        if (defaultPipeline is not null)
            project.AzuredevopsTrigger = new WebhookTriggerConfig { DefaultPipeline = defaultPipeline };

        return new AzureDevOpsWorkItemPoller(
            "proj", project, factory.Object,
            new Mock<ITicketStatusTransitioner>().Object,
            NullLogger<AzureDevOpsWorkItemPoller>.Instance);
    }
}
