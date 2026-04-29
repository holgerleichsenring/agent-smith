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
    private readonly ILogger _logger = loggerFactory.CreateLogger<TicketProviderFactory>();

    public ITicketProvider Create(TicketConfig config)
    {
        var type = config.Type.ToLowerInvariant();
        _logger.LogDebug("TicketProviderFactory.Create: type={Type}", type);
        var provider = type switch
        {
            "azuredevops" => (ITicketProvider)CreateAzureDevOps(config),
            "github" => CreateGitHub(config),
            "jira" => CreateJira(config),
            "gitlab" => CreateGitLab(config),
            _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}")
        };
        _logger.LogDebug("TicketProviderFactory.Create: returning {Provider}", provider.GetType().Name);
        return provider;
    }

    private AzureDevOpsTicketProvider CreateAzureDevOps(TicketConfig config)
    {
        var orgUrl = $"https://dev.azure.com/{config.Organization}";
        _logger.LogDebug("CreateAzureDevOps: org={Org} project={Project}", config.Organization, config.Project);
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var loader = new AzureDevOpsAttachmentLoader(
            orgUrl, config.Project!, token,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsAttachmentLoader>());
        return new AzureDevOpsTicketProvider(
            orgUrl, config.Project!, token, loader,
            loggerFactory.CreateLogger<AzureDevOpsTicketProvider>(),
            openStates: config.OpenStates.Count > 0 ? config.OpenStates : null,
            doneStatus: config.DoneStatus,
            extraFields: config.ExtraFields.Count > 0 ? config.ExtraFields : null);
    }

    private GitHubTicketProvider CreateGitHub(TicketConfig config)
    {
        _logger.LogDebug("CreateGitHub: url={Url}", config.Url);
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var loader = new GitHubAttachmentLoader(
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubAttachmentLoader>());
        return new GitHubTicketProvider(config.Url!, token, loader);
    }

    private JiraTicketProvider CreateJira(TicketConfig config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        _logger.LogDebug("CreateJira: url={Url} project={Project}", url, config.Project);
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        return new JiraTicketProvider(url, email, token, httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<JiraTicketProvider>(),
            doneStatus: config.DoneStatus,
            closeTransitionName: config.CloseTransitionName,
            projectKey: config.Project);
    }

    private GitLabTicketProvider CreateGitLab(TicketConfig config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var projectPath = config.Project ?? secrets.GetRequired("GITLAB_PROJECT");
        _logger.LogDebug("CreateGitLab: baseUrl={BaseUrl} project={Project}", baseUrl, projectPath);
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var escapedPath = Uri.EscapeDataString(projectPath);
        var httpClient = httpClientFactory.CreateClient();
        var loader = new GitLabAttachmentLoader(
            baseUrl, escapedPath, token, httpClient,
            loggerFactory.CreateLogger<GitLabAttachmentLoader>());
        return new GitLabTicketProvider(baseUrl, escapedPath, token, httpClient, loader);
    }
}
