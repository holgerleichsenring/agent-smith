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

public sealed class JiraIssuePollerTests
{
    [Fact]
    public async Task PollAsync_NoPendingTickets_ReturnsEmpty()
    {
        var sut = Build(pendingTickets: []);
        var result = await sut.PollAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PollAsync_WithPendingTickets_ReturnsClaimRequestsTaggedJira()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("PROJ-42"), "t1", "", null, "To Do", "Jira"),
            new Ticket(new TicketId("PROJ-43"), "t2", "", null, "To Do", "Jira")
        };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "fix-bug");

        var result = await sut.PollAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Platform.Should().Be("Jira");
        result[0].ProjectName.Should().Be("proj");
        result[0].PipelineName.Should().Be("fix-bug");
        result[0].TicketId.Value.Should().Be("PROJ-42");
    }

    [Fact]
    public async Task PollAsync_NoTriggerConfig_FallsBackToFixBug()
    {
        var tickets = new[] { new Ticket(new TicketId("PROJ-1"), "t", "", null, "To Do", "Jira") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: null);

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task PollAsync_JiraTriggerOverride_UsesConfiguredPipeline()
    {
        var tickets = new[] { new Ticket(new TicketId("PROJ-1"), "t", "", null, "To Do", "Jira") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "security-scan");

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("security-scan");
    }

    [Fact]
    public async Task PollAsync_TicketWithMatchingLabel_RoutesToMappedPipeline()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("PROJ-1"), "t", "", null, "To Do", "Jira", labels: ["security-review"])
        };
        var sut = Build(
            pendingTickets: tickets,
            defaultPipeline: "fix-bug",
            pipelineFromLabel: new() { ["security-review"] = "security-scan" });

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("security-scan");
    }

    [Fact]
    public async Task PollAsync_TicketWithNoMatchingLabel_FallsBackToFixBug()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("PROJ-1"), "t", "", null, "To Do", "Jira", labels: ["unrelated"])
        };
        var sut = Build(
            pendingTickets: tickets,
            defaultPipeline: "fix-bug",
            pipelineFromLabel: new() { ["security-review"] = "security-scan" });

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("fix-bug");
    }

    private static JiraIssuePoller Build(
        Ticket[] pendingTickets,
        string? defaultPipeline = "fix-bug",
        Dictionary<string, string>? pipelineFromLabel = null)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingTickets);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(provider.Object);

        var project = new ProjectConfig();
        if (defaultPipeline is not null || pipelineFromLabel is not null)
            project.JiraTrigger = new JiraTriggerConfig
            {
                DefaultPipeline = defaultPipeline ?? "fix-bug",
                PipelineFromLabel = pipelineFromLabel ?? new()
            };

        return new JiraIssuePoller(
            "proj", project, factory.Object,
            new Mock<ITicketStatusTransitioner>().Object,
            NullLogger<JiraIssuePoller>.Instance);
    }
}
