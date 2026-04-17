using AgentSmith.Infrastructure.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
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
        var loader = new AzureDevOpsAttachmentLoader(
            orgUrl, config.Project!, token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsAttachmentLoader>());
        return new AzureDevOpsTicketProvider(
            orgUrl, config.Project!, token, loader,
            openStates: config.OpenStates.Count > 0 ? config.OpenStates : null,
            doneStatus: config.DoneStatus,
            extraFields: config.ExtraFields.Count > 0 ? config.ExtraFields : null);
    }

    private GitHubTicketProvider CreateGitHub(TicketConfig config)
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var loader = new GitHubAttachmentLoader(
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubAttachmentLoader>());
        return new GitHubTicketProvider(config.Url!, token, loader);
    }

    private JiraTicketProvider CreateJira(TicketConfig config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        return new JiraTicketProvider(url, email, token, httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketProvider>(),
            doneStatus: config.DoneStatus,
            closeTransitionName: config.CloseTransitionName);
    }

    private GitLabTicketProvider CreateGitLab(TicketConfig config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var projectPath = config.Project ?? secrets.GetRequired("GITLAB_PROJECT");
        var escapedPath = Uri.EscapeDataString(projectPath);
        var httpClient = httpClientFactory.CreateClient();
        var loader = new GitLabAttachmentLoader(
            baseUrl, escapedPath, token, httpClient,
            loggerFactory.CreateLogger<GitLabAttachmentLoader>());
        return new GitLabTicketProvider(baseUrl, escapedPath, token, httpClient, loader);
    }
}
