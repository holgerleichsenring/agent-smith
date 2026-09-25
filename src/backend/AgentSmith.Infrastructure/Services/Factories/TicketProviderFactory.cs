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

    public ITicketProvider Create(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => (ITicketProvider)CreateAzureDevOps(config),
        TrackerType.GitHub => CreateGitHub(config),
        TrackerType.Jira => CreateJira(config),
        TrackerType.GitLab => CreateGitLab(config),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}")
    };

    /// <summary>
    /// 2026-09-25-8e51e: the same connections, built for the one write that reads first. Jira
    /// answers with a refusal that names the round trip rather than with nothing — see
    /// <see cref="JiraTicketRewriter"/>.
    /// </summary>
    public ITicketRewriter CreateRewriter(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => new AzureDevOpsTicketRewriter(
            new AzureDevOpsTicketConnection(
                $"https://dev.azure.com/{config.Organization}", config.Project!,
                secrets.GetRequired("AZURE_DEVOPS_TOKEN")),
            loggerFactory.CreateLogger<AzureDevOpsTicketRewriter>()),
        TrackerType.GitHub => new GitHubTicketRewriter(
            new GitHubTicketConnection(config.Url!, secrets.GetRequired("GITHUB_TOKEN")),
            loggerFactory.CreateLogger<GitHubTicketRewriter>()),
        TrackerType.Jira => new JiraTicketRewriter(),
        TrackerType.GitLab => new GitLabTicketRewriter(
            new GitLabTicketConnection(
                secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl,
                Uri.EscapeDataString(config.Project ?? secrets.GetRequired("GITLAB_PROJECT")),
                secrets.GetRequired("GITLAB_TOKEN")),
            httpClientFactory.CreateClient(), loggerFactory.CreateLogger<GitLabTicketRewriter>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };

    private AzureDevOpsTicketProvider CreateAzureDevOps(TrackerConnection config)
    {
        var orgUrl = $"https://dev.azure.com/{config.Organization}";
        _logger.LogDebug("CreateAzureDevOps: org={Org} project={Project}", config.Organization, config.Project);
        var token = secrets.GetRequired("AZURE_DEVOPS_TOKEN");
        var connection = new AzureDevOpsTicketConnection(orgUrl, config.Project!, token);
        var loader = new AzureDevOpsAttachmentLoader(
            connection,
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsAttachmentLoader>());
        return new AzureDevOpsTicketProvider(
            connection, loader,
            new AzureDevOpsFieldMapper(),
            loggerFactory.CreateLogger<AzureDevOpsTicketProvider>(),
            openStates: config.OpenStates.Count > 0 ? config.OpenStates : null,
            doneStatus: config.DoneStatus,
            extraFields: config.ExtraFields.Count > 0 ? config.ExtraFields : null);
    }

    private GitHubTicketProvider CreateGitHub(TrackerConnection config)
    {
        _logger.LogDebug("CreateGitHub: url={Url}", config.Url);
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var connection = new GitHubTicketConnection(config.Url!, token);
        var loader = new GitHubAttachmentLoader(
            httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitHubAttachmentLoader>());
        return new GitHubTicketProvider(connection, loader,
            new GitHubFieldMapper(),
            loggerFactory.CreateLogger<GitHubTicketProvider>());
    }

    private JiraTicketProvider CreateJira(TrackerConnection config)
    {
        var url = config.Url ?? secrets.GetRequired("JIRA_URL");
        _logger.LogDebug("CreateJira: url={Url} project={Project}", url, config.Project);
        var email = secrets.GetRequired("JIRA_EMAIL");
        var token = secrets.GetRequired("JIRA_TOKEN");
        var connection = new JiraTicketConnection(
            url, email, token, config.Project, config.Endpoints, ParentLinkType: config.ParentLinkType);
        return new JiraTicketProvider(connection, httpClientFactory.CreateClient(),
            new JiraFieldMapper(),
            loggerFactory.CreateLogger<JiraTicketProvider>(),
            doneStatus: config.DoneStatus,
            closeTransitionName: config.CloseTransitionName);
    }

    private GitLabTicketProvider CreateGitLab(TrackerConnection config)
    {
        var baseUrl = secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl;
        var projectPath = config.Project ?? secrets.GetRequired("GITLAB_PROJECT");
        _logger.LogDebug("CreateGitLab: baseUrl={BaseUrl} project={Project}", baseUrl, projectPath);
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var escapedPath = Uri.EscapeDataString(projectPath);
        var httpClient = httpClientFactory.CreateClient();
        var connection = new GitLabTicketConnection(baseUrl, escapedPath, token);
        var loader = new GitLabAttachmentLoader(
            connection, httpClient,
            loggerFactory.CreateLogger<GitLabAttachmentLoader>());
        return new GitLabTicketProvider(connection, httpClient, loader,
            new GitLabFieldMapper(),
            loggerFactory.CreateLogger<GitLabTicketProvider>());
    }
}
