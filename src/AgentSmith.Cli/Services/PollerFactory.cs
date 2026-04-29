using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Builds platform-specific IEventPoller instances from AgentSmithConfig.
/// Extracted from ServerCommand to keep that class under the 120-line limit (p0101).
/// </summary>
internal static class PollerFactory
{
    public static IEnumerable<IEventPoller> Build(
        IServiceProvider provider, AgentSmithConfig config)
    {
        var ticketFactory = provider.GetRequiredService<ITicketProviderFactory>();
        var transitionerFactory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();
        var resolver = provider.GetRequiredService<IPipelineConfigResolver>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ServerCommand.BuildPollers");

        foreach (var (name, project) in config.Projects)
        {
            if (!project.Polling.Enabled) continue;

            var poller = BuildOne(name, project, ticketFactory, transitionerFactory, resolver, loggerFactory);
            if (poller is null)
            {
                logger.LogWarning(
                    "Polling enabled for project {Project} but ticket type '{Type}' has no poller — skipping",
                    name, project.Tickets.Type);
                continue;
            }
            yield return poller;
        }
    }

    private static IEventPoller? BuildOne(
        string name, ProjectConfig project,
        ITicketProviderFactory ticketFactory,
        ITicketStatusTransitionerFactory transitionerFactory,
        IPipelineConfigResolver resolver,
        ILoggerFactory loggerFactory)
    {
        var transitioner = transitionerFactory.Create(project.Tickets);
        return project.Tickets.Type.ToLowerInvariant() switch
        {
            "github" => new GitHubIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<GitHubIssuePoller>()),
            "azuredevops" => new AzureDevOpsWorkItemPoller(
                name, project, ticketFactory, transitioner, resolver,
                loggerFactory.CreateLogger<AzureDevOpsWorkItemPoller>()),
            "gitlab" => new GitLabIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<GitLabIssuePoller>()),
            "jira" => new JiraIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<JiraIssuePoller>()),
            _ => null
        };
    }
}
