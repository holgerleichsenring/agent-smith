using AgentSmith.Application.Services.Polling;
using AgentSmith.Cli.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cli.Commands;

public sealed class ServerCommandBuildPollersTests
{
    [Fact]
    public void BuildPollers_GitHubProject_RegistersGitHubIssuePoller()
    {
        var pollers = Build("github").ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<GitHubIssuePoller>();
        pollers[0].PlatformName.Should().Be("GitHub");
    }

    [Fact]
    public void BuildPollers_AzureDevOpsProject_RegistersAzureDevOpsWorkItemPoller()
    {
        var pollers = Build("azuredevops").ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<AzureDevOpsWorkItemPoller>();
        pollers[0].PlatformName.Should().Be("AzureDevOps");
    }

    [Fact]
    public void BuildPollers_GitLabProject_RegistersGitLabIssuePoller()
    {
        var pollers = Build("gitlab").ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<GitLabIssuePoller>();
        pollers[0].PlatformName.Should().Be("GitLab");
    }

    [Fact]
    public void BuildPollers_JiraProject_RegistersJiraIssuePoller()
    {
        var pollers = Build("jira").ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<JiraIssuePoller>();
        pollers[0].PlatformName.Should().Be("Jira");
    }

    [Fact]
    public void BuildPollers_UnsupportedTicketType_RegistersNothing()
    {
        var pollers = Build("ollama-local").ToList();
        pollers.Should().BeEmpty();
    }

    [Fact]
    public void BuildPollers_PollingDisabled_RegistersNothing()
    {
        var pollers = Build("github", pollingEnabled: false).ToList();
        pollers.Should().BeEmpty();
    }

    [Fact]
    public void BuildPollers_TypeMatchIsCaseInsensitive()
    {
        var pollers = Build("GitHub").ToList();
        pollers.Should().HaveCount(1);
        pollers[0].Should().BeOfType<GitHubIssuePoller>();
    }

    private static IEnumerable<IEventPoller> Build(
        string ticketType, bool pollingEnabled = true)
    {
        var ticketFactory = new Mock<ITicketProviderFactory>();
        var transitionerFactory = new Mock<ITicketStatusTransitionerFactory>();
        transitionerFactory.Setup(f => f.Create(It.IsAny<TicketConfig>()))
            .Returns(new Mock<ITicketStatusTransitioner>().Object);

        var services = new ServiceCollection();
        services.AddSingleton(ticketFactory.Object);
        services.AddSingleton(transitionerFactory.Object);
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        var provider = services.BuildServiceProvider();

        var config = new AgentSmithConfig();
        config.Projects["test"] = new ProjectConfig
        {
            Tickets = new TicketConfig { Type = ticketType },
            Polling = new PollingConfig { Enabled = pollingEnabled }
        };

        return ServerCommand.BuildPollers(provider, config);
    }
}
