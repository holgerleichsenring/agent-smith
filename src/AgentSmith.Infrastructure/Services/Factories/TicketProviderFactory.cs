using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate ITicketProvider based on configuration type.
/// </summary>
public sealed class TicketProviderFactory(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ITicketProviderFactory
{
    public ITicketProvider Create(TicketConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "azuredevops" => CreateAzureDevOps(config),
            "github" => CreateGitHub(config),
            "jira" => CreateJira(config),
            "gitlab" => CreateGitLab(config),
            _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}")
        };
    }

    private AzureDevOpsTicketProvider CreateAzureDevOps(TicketConfig config)
    {
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var orgUrl = $"https://dev.azure.com/{config.Organization}";
        return new AzureDevOpsTicketProvider(orgUrl, config.Project!, token);
    }

    private GitHubTicketProvider CreateGitHub(TicketConfig config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        return new GitHubTicketProvider(config.Url!, token);
    }

    private JiraTicketProvider CreateJira(TicketConfig config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        return new JiraTicketProvider(url, email, token, httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketProvider>());
    }

    private GitLabTicketProvider CreateGitLab(TicketConfig config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var projectPath = config.Project ?? secrets.GetRequired("GITLAB_PROJECT");
        return new GitLabTicketProvider(baseUrl, Uri.EscapeDataString(projectPath),
            token, httpClientFactory.CreateClient());
    }
}
