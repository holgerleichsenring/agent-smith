using AgentSmith.Application.Services.Polling;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
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

    /// <summary>
    /// End-to-end smoke: real YAML on disk → real YamlConfigurationLoader → BuildPollers
    /// → all four platforms register, in order, with no Redis/network dependency.
    /// Backstops p0099 wiring against config-loader regressions.
    /// </summary>
    [Fact]
    public void BuildPollers_LoadsYamlConfig_RegistersAllFourPlatformPollers()
    {
        var yaml = """
            projects:
              gh:
                source: { type: GitHub, url: https://github.com/o/r }
                tickets: { type: github, url: https://github.com/o/r, auth: token }
                pipeline: fix-bug
                polling: { enabled: true }
              azdo:
                source: { type: AzureDevOps, url: https://dev.azure.com/o/p/_git/r }
                tickets: { type: azuredevops, organization: https://dev.azure.com/o, project: p, auth: pat }
                pipeline: fix-bug
                polling: { enabled: true }
              gl:
                source: { type: GitLab, url: https://gitlab.com/g/r }
                tickets: { type: gitlab, project: g/r, auth: token }
                pipeline: fix-bug
                polling: { enabled: true }
              jr:
                source: { type: GitHub, url: https://github.com/o/r }
                tickets: { type: jira, url: https://jira.example, project: PROJ, auth: token }
                pipeline: fix-bug
                polling: { enabled: true }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-dry-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);

        try
        {
            var config = new YamlConfigurationLoader().LoadConfig(path);

            var ticketFactory = new Mock<ITicketProviderFactory>();
            var transitionerFactory = new Mock<ITicketStatusTransitionerFactory>();
            transitionerFactory.Setup(f => f.Create(It.IsAny<TicketConfig>()))
                .Returns(new Mock<ITicketStatusTransitioner>().Object);

            var services = new ServiceCollection();
            services.AddSingleton(ticketFactory.Object);
            services.AddSingleton(transitionerFactory.Object);
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

            var pollers = PollerFactory.Build(services.BuildServiceProvider(), config).ToList();

            pollers.Should().HaveCount(4);
            pollers.Select(p => p.PlatformName).Should().BeEquivalentTo(
                new[] { "GitHub", "AzureDevOps", "GitLab", "Jira" });
            pollers.OfType<GitHubIssuePoller>().Should().HaveCount(1);
            pollers.OfType<AzureDevOpsWorkItemPoller>().Should().HaveCount(1);
            pollers.OfType<GitLabIssuePoller>().Should().HaveCount(1);
            pollers.OfType<JiraIssuePoller>().Should().HaveCount(1);
        }
        finally
        {
            File.Delete(path);
        }
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

        return PollerFactory.Build(provider, config);
    }
}
