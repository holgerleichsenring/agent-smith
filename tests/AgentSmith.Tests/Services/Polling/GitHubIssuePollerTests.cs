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

public sealed class GitHubIssuePollerTests
{
    [Fact]
    public async Task PollAsync_NoPendingTickets_ReturnsEmpty()
    {
        var sut = Build(pendingTickets: []);
        var result = await sut.PollAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PollAsync_WithPendingTickets_ReturnsClaimRequests()
    {
        var tickets = new Ticket[]
        {
            new(new TicketId("1"), "t1", "", null, "open", "GitHub"),
            new(new TicketId("2"), "t2", "", null, "open", "GitHub")
        };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "fix-bug");

        var result = await sut.PollAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(
            new { Platform = "GitHub", ProjectName = "proj", PipelineName = "fix-bug" },
            options => options.ExcludingMissingMembers());
        result[0].TicketId.Value.Should().Be("1");
    }

    [Fact]
    public async Task PollAsync_NoGithubTrigger_DefaultsToFixBug()
    {
        var tickets = new[] { new Ticket(new TicketId("1"), "t", "", null, "open", "GitHub") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: null);

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("fix-bug");
    }

    private static GitHubIssuePoller Build(Ticket[] pendingTickets, string? defaultPipeline = "fix-bug")
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingTickets);

        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TicketConfig>())).Returns(provider.Object);

        var project = new ProjectConfig();
        if (defaultPipeline is not null)
            project.GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = defaultPipeline };

        return new GitHubIssuePoller(
            "proj", project, factory.Object,
            new Mock<ITicketStatusTransitioner>().Object,
            NullLogger<GitHubIssuePoller>.Instance);
    }
}
