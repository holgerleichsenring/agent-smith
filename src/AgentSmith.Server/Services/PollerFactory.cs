using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

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
        var logger = loggerFactory.CreateLogger("AgentSmith.Server.PollerFactory");

        logger.LogInformation(
            "PollerFactory.Build: scanning {ProjectCount} projects for polling=enabled",
            config.Projects.Count);

        foreach (var (name, project) in config.Projects)
        {
            if (!project.Polling.Enabled)
            {
                logger.LogDebug("  skip {Project}: polling disabled", name);
                continue;
            }

            IEventPoller? poller;
            try
            {
                poller = BuildOne(name, project, ticketFactory, transitionerFactory, resolver, loggerFactory);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Polling enabled for project {Project} but poller could not be built (Tickets.Type={Type}): {Message}",
                    name, project.Tracker.Type, ex.Message);
                continue;
            }

            if (poller is null)
            {
                logger.LogWarning(
                    "Polling enabled for project {Project} but ticket type '{Type}' has no poller — skipping",
                    name, project.Tracker.Type);
                continue;
            }
            logger.LogInformation("  built poller for {Project} (Tickets.Type={Type})", name, project.Tracker.Type);
            yield return poller;
        }
    }

    private static IEventPoller? BuildOne(
        string name, ResolvedProject project,
        ITicketProviderFactory ticketFactory,
        ITicketStatusTransitionerFactory transitionerFactory,
        IPipelineConfigResolver resolver,
        ILoggerFactory loggerFactory)
    {
        var transitioner = transitionerFactory.Create(project.Tracker);
        return project.Tracker.Type switch
        {
            TrackerType.GitHub => new GitHubIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<GitHubIssuePoller>()),
            TrackerType.AzureDevOps => new AzureDevOpsWorkItemPoller(
                name, project, ticketFactory, transitioner, resolver,
                loggerFactory.CreateLogger<AzureDevOpsWorkItemPoller>()),
            TrackerType.GitLab => new GitLabIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<GitLabIssuePoller>()),
            TrackerType.Jira => new JiraIssuePoller(
                name, project, ticketFactory, transitioner,
                loggerFactory.CreateLogger<JiraIssuePoller>()),
            _ => null
        };
    }
}
