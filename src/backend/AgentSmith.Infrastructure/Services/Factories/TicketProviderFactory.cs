using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// Creates the appropriate ITicketProvider based on configuration type. The connection records
/// themselves come from <see cref="TrackerConnections"/>, which all three capabilities share.
/// </summary>
public sealed class TicketProviderFactory(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : ITicketProviderFactory
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TicketProviderFactory>();
    private readonly TrackerConnections _connections = new(secrets);

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
            _connections.AzureDevOps(config), loggerFactory.CreateLogger<AzureDevOpsTicketRewriter>()),
        TrackerType.GitHub => new GitHubTicketRewriter(
            _connections.GitHub(config), loggerFactory.CreateLogger<GitHubTicketRewriter>()),
        TrackerType.Jira => new JiraTicketRewriter(),
        TrackerType.GitLab => new GitLabTicketRewriter(
            _connections.GitLab(config), httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabTicketRewriter>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };

    /// <summary>
    /// 2026-09-27-5c1ea: the same connections, built for the one read a person drives by typing.
    /// GitHub's search takes the Octokit client as its interface so the request it builds is
    /// assertable; the others take the shared HttpClient as their siblings do.
    /// </summary>
    public ITicketSearch CreateSearch(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => new AzureDevOpsTicketSearch(
            _connections.AzureDevOps(config), TrackerConnections.OpenStates(config),
            TrackerConnections.ExtraFields(config),
            loggerFactory.CreateLogger<AzureDevOpsTicketSearch>()),
        TrackerType.GitHub => new GitHubTicketSearch(
            _connections.GitHubClient(config), _connections.GitHub(config),
            loggerFactory.CreateLogger<GitHubTicketSearch>()),
        TrackerType.Jira => new JiraTicketSearch(
            _connections.Jira(config), httpClientFactory.CreateClient(), new JiraFieldMapper(),
            loggerFactory.CreateLogger<JiraTicketSearch>()),
        TrackerType.GitLab => new GitLabTicketSearch(
            _connections.GitLab(config), httpClientFactory.CreateClient(), new GitLabFieldMapper(),
            loggerFactory.CreateLogger<GitLabTicketSearch>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };

    private AzureDevOpsTicketProvider CreateAzureDevOps(TrackerConnection config)
    {
        _logger.LogDebug("CreateAzureDevOps: org={Org} project={Project}", config.Organization, config.Project);
        var connection = _connections.AzureDevOps(config);
        var loader = new AzureDevOpsAttachmentLoader(
            connection, httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<AzureDevOpsAttachmentLoader>());
        return new AzureDevOpsTicketProvider(
            connection, loader, new AzureDevOpsFieldMapper(),
            loggerFactory.CreateLogger<AzureDevOpsTicketProvider>(),
            openStates: TrackerConnections.OpenStates(config),
            doneStatus: config.DoneStatus,
            extraFields: TrackerConnections.ExtraFields(config));
    }

    private GitHubTicketProvider CreateGitHub(TrackerConnection config)
    {
        _logger.LogDebug("CreateGitHub: url={Url}", config.Url);
        var loader = new GitHubAttachmentLoader(
            httpClientFactory.CreateClient(), loggerFactory.CreateLogger<GitHubAttachmentLoader>());
        return new GitHubTicketProvider(_connections.GitHub(config), loader,
            new GitHubFieldMapper(), loggerFactory.CreateLogger<GitHubTicketProvider>());
    }

    private JiraTicketProvider CreateJira(TrackerConnection config)
    {
        _logger.LogDebug("CreateJira: url={Url} project={Project}", config.Url, config.Project);
        return new JiraTicketProvider(
            _connections.Jira(config), httpClientFactory.CreateClient(), new JiraFieldMapper(),
            loggerFactory.CreateLogger<JiraTicketProvider>(),
            doneStatus: config.DoneStatus, closeTransitionName: config.CloseTransitionName);
    }

    private GitLabTicketProvider CreateGitLab(TrackerConnection config)
    {
        _logger.LogDebug("CreateGitLab: project={Project}", config.Project);
        var connection = _connections.GitLab(config);
        var httpClient = httpClientFactory.CreateClient();
        var loader = new GitLabAttachmentLoader(
            connection, httpClient, loggerFactory.CreateLogger<GitLabAttachmentLoader>());
        return new GitLabTicketProvider(connection, httpClient, loader,
            new GitLabFieldMapper(), loggerFactory.CreateLogger<GitLabTicketProvider>());
    }
}
