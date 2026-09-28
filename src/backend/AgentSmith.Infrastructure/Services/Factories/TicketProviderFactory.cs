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
    private readonly TicketCapabilities _capabilities =
        new(new TrackerConnections(secrets), httpClientFactory, loggerFactory);

    public ITicketProvider Create(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => (ITicketProvider)CreateAzureDevOps(config),
        TrackerType.GitHub => CreateGitHub(config),
        TrackerType.Jira => CreateJira(config),
        TrackerType.GitLab => CreateGitLab(config),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}")
    };

    /// <summary>
    /// 2026-09-25-8e51e, 2026-09-27-5c1ea: the capabilities BESIDE the provider — the write that
    /// reads first, and the text search. Their switches live in
    /// <see cref="TicketCapabilities"/>: a fourth one would not fit here, and a partial of this
    /// type is the one thing the per-file limit exists to prevent.
    /// </summary>
    public ITicketRewriter CreateRewriter(TrackerConnection config) =>
        _capabilities.Rewriter(config);

    public ITicketSearch CreateSearch(TrackerConnection config) => _capabilities.Search(config);

    public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) =>
        _capabilities.LinkedWork(config);

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
