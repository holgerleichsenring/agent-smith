using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the platform-specific ITicketStatusTransitioner. p95a shipped GitHub;
/// p95b adds GitLab, AzureDevOps, and Jira. 2026-10-02-5f89a: the connection records come from
/// the same <see cref="TrackerConnections"/> the ticket provider reads, so a transition
/// authenticates exactly as the provider does — with the tracker's own auth secret.
/// </summary>
public sealed class TicketStatusTransitionerFactory(
    ICredentialResolver credentials,
    JiraWorkflowCatalog jiraCatalog,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ITicketStatusTransitionerFactory
{
    private const string DefaultJiraProjectKey = "default";
    private readonly TrackerConnections _connections = new(credentials);

    public ITicketStatusTransitioner Create(TrackerConnection config)
        => config.Type switch
        {
            TrackerType.GitHub => CreateGitHub(config),
            TrackerType.GitLab => CreateGitLab(config),
            TrackerType.AzureDevOps => CreateAzureDevOps(config),
            TrackerType.Jira => CreateJira(config),
            _ => throw new NotSupportedException(
                $"ITicketStatusTransitioner not implemented for platform '{config.Type}'")
        };

    private GitHubTicketStatusTransitioner CreateGitHub(TrackerConnection config) =>
        new(_connections.GitHub(config),
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubTicketStatusTransitioner>());

    private GitLabTicketStatusTransitioner CreateGitLab(TrackerConnection config) =>
        new(_connections.GitLab(config),
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabTicketStatusTransitioner>());

    private AzureDevOpsTicketStatusTransitioner CreateAzureDevOps(TrackerConnection config) =>
        new(_connections.AzureDevOps(config),
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsTicketStatusTransitioner>());

    // A Jira tracker with no project still transitions under the 'default' key it always had.
    private JiraTicketStatusTransitioner CreateJira(TrackerConnection config)
    {
        var projectKey = config.Project ?? DefaultJiraProjectKey;
        var connection = _connections.Jira(config) with
        {
            ProjectKey = projectKey,
            LifecycleStatusMap = BuildLifecycleMap(config.LifecycleStatusNames, projectKey),
        };
        return new JiraTicketStatusTransitioner(
            connection, jiraCatalog,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketStatusTransitioner>());
    }

    private JiraLifecycleStatusMap BuildLifecycleMap(
        IReadOnlyDictionary<string, string> configured, string projectKey)
    {
        if (configured.Count == 0) return JiraLifecycleStatusMap.Empty;
        var logger = loggerFactory.CreateLogger<TicketStatusTransitionerFactory>();
        var names = new Dictionary<TicketLifecycleStatus, string>();
        foreach (var (key, statusName) in configured)
        {
            if (LifecycleLabels.TryParseName(key, out var status))
                names[status] = statusName;
            else
                logger.LogWarning(
                    "Jira '{Project}' lifecycle_status_names: unknown lifecycle key '{Key}' ignored",
                    projectKey, key);
        }
        return new JiraLifecycleStatusMap(names);
    }
}
