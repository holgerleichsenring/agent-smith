using AgentSmith.Contracts.Models.Configuration;
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
    public ITicketStatusTransitioner Create(TicketConfig config)
        => config.Type.ToLowerInvariant() switch
        {
            "github" => CreateGitHub(config),
            "gitlab" => CreateGitLab(config),
            "azuredevops" => CreateAzureDevOps(config),
            "jira" => CreateJira(config),
            _ => throw new NotSupportedException(
                $"ITicketStatusTransitioner not implemented for platform '{config.Type}'")
        };

    private GitHubTicketStatusTransitioner CreateGitHub(TicketConfig config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        return new GitHubTicketStatusTransitioner(
            config.Url ?? throw new ArgumentException("GitHub URL required"),
            token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubTicketStatusTransitioner>());
    }

    private GitLabTicketStatusTransitioner CreateGitLab(TicketConfig config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var projectPath = Uri.EscapeDataString(
            config.Project ?? secrets.GetRequired("GITLAB_PROJECT"));
        return new GitLabTicketStatusTransitioner(
            baseUrl, projectPath, token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabTicketStatusTransitioner>());
    }

    private AzureDevOpsTicketStatusTransitioner CreateAzureDevOps(TicketConfig config)
    {
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var orgUrl = $"https://dev.azure.com/{config.Organization}";
        return new AzureDevOpsTicketStatusTransitioner(
            orgUrl, config.Project!, token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsTicketStatusTransitioner>());
    }

    private JiraTicketStatusTransitioner CreateJira(TicketConfig config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        var projectKey = config.Project ?? secrets.GetOptional("JIRA_PROJECT") ?? "default";
        return new JiraTicketStatusTransitioner(
            url, email, token, projectKey, jiraCatalog,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketStatusTransitioner>());
    }
}
