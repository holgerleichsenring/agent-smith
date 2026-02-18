using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Configuration;
using AgentSmith.Infrastructure.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Factories;

/// <summary>
/// Creates the appropriate ITicketProvider based on configuration type.
/// </summary>
public sealed class TicketProviderFactory(
    SecretsProvider secrets,
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
        return new JiraTicketProvider(url, email, token, new HttpClient(),
            loggerFactory.CreateLogger<JiraTicketProvider>());
    }

    private GitLabTicketProvider CreateGitLab(TicketConfig config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? "https://gitlab.com";
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var projectPath = config.Project ?? secrets.GetRequired("GITLAB_PROJECT");
        return new GitLabTicketProvider(baseUrl, Uri.EscapeDataString(projectPath),
            token, new HttpClient());
    }
}
