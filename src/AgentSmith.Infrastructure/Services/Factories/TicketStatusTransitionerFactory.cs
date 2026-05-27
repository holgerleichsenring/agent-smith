using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the platform-specific ITicketStatusTransitioner. p95a shipped GitHub;
/// p95b adds GitLab, AzureDevOps, and Jira.
/// </summary>
public sealed class TicketStatusTransitionerFactory(
    SecretsProvider secrets,
    JiraWorkflowCatalog jiraCatalog,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ITicketStatusTransitionerFactory
{
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

    private GitHubTicketStatusTransitioner CreateGitHub(TrackerConnection config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var connection = new GitHubTicketConnection(
            config.Url ?? throw new ArgumentException("GitHub URL required"),
            token);
        return new GitHubTicketStatusTransitioner(
            connection,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubTicketStatusTransitioner>());
    }

    private GitLabTicketStatusTransitioner CreateGitLab(TrackerConnection config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var projectPath = Uri.EscapeDataString(
            config.Project ?? secrets.GetRequired("GITLAB_PROJECT"));
        var connection = new GitLabTicketConnection(baseUrl, projectPath, token);
        return new GitLabTicketStatusTransitioner(
            connection,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabTicketStatusTransitioner>());
    }

    private AzureDevOpsTicketStatusTransitioner CreateAzureDevOps(TrackerConnection config)
    {
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var orgUrl = $"https://dev.azure.com/{config.Organization}";
        var connection = new AzureDevOpsTicketConnection(orgUrl, config.Project!, token);
        return new AzureDevOpsTicketStatusTransitioner(
            connection,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsTicketStatusTransitioner>());
    }

    private JiraTicketStatusTransitioner CreateJira(TrackerConnection config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        var projectKey = config.Project ?? secrets.GetOptional("JIRA_PROJECT") ?? "default";
        var connection = new JiraTicketConnection(url, email, token, projectKey);
        return new JiraTicketStatusTransitioner(
            connection, jiraCatalog,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketStatusTransitioner>());
    }
}
