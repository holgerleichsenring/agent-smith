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

public sealed class GitLabIssuePollerTests
{
    [Fact]
    public async Task PollAsync_NoPendingTickets_ReturnsEmpty()
    {
        var sut = Build(pendingTickets: []);
        var result = await sut.PollAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PollAsync_WithPendingTickets_ReturnsClaimRequestsTaggedGitLab()
    {
        var tickets = new[]
        {
            new Ticket(new TicketId("17"), "t1", "", null, "opened", "GitLab"),
            new Ticket(new TicketId("18"), "t2", "", null, "opened", "GitLab")
        };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "fix-bug");

        var result = await sut.PollAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Platform.Should().Be("GitLab");
        result[0].ProjectName.Should().Be("proj");
        result[0].PipelineName.Should().Be("fix-bug");
        result[0].TicketId.Value.Should().Be("17");
    }

    [Fact]
    public async Task PollAsync_NoTriggerConfig_FallsBackToFixBug()
    {
        var tickets = new[] { new Ticket(new TicketId("1"), "t", "", null, "opened", "GitLab") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: null);

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("fix-bug");
    }

    [Fact]
    public async Task PollAsync_GitlabTriggerOverride_UsesConfiguredPipeline()
    {
        var tickets = new[] { new Ticket(new TicketId("1"), "t", "", null, "opened", "GitLab") };
        var sut = Build(pendingTickets: tickets, defaultPipeline: "security-scan");

        var result = await sut.PollAsync(CancellationToken.None);

        result[0].PipelineName.Should().Be("security-scan");
    }

    private static GitLabIssuePoller Build(
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
            project.GitlabTrigger = new WebhookTriggerConfig { DefaultPipeline = defaultPipeline };

        return new GitLabIssuePoller(
            "proj", project, factory.Object,
            new Mock<ITicketStatusTransitioner>().Object,
            NullLogger<GitLabIssuePoller>.Instance);
    }
}
