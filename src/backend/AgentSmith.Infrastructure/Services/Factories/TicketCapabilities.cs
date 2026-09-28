using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-09-28-1da5c: the capability ports built BESIDE a ticket provider — the rewrite, the search,
/// and what a tracker links to a ticket.
/// <para>
/// Extracted from <see cref="TicketProviderFactory"/> when a fourth capability would not fit: each
/// is a documented four-armed switch of a dozen lines, the factory sat five under the per-file
/// limit and is not in the baseline, and a partial of it is the one thing that limit exists to
/// prevent. The factory keeps the construction that is genuinely its own.
/// </para>
/// </summary>
internal sealed class TicketCapabilities(
    TrackerConnections connections,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory)
{
    /// <summary>
    /// 2026-09-25-8e51e: the same connections, built for the one write that reads first. Jira
    /// answers with a refusal that names the round trip rather than with nothing — see
    /// <see cref="JiraTicketRewriter"/>.
    /// </summary>
    public ITicketRewriter Rewriter(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => new AzureDevOpsTicketRewriter(
            connections.AzureDevOps(config), loggerFactory.CreateLogger<AzureDevOpsTicketRewriter>()),
        TrackerType.GitHub => new GitHubTicketRewriter(
            connections.GitHub(config), loggerFactory.CreateLogger<GitHubTicketRewriter>()),
        TrackerType.Jira => new JiraTicketRewriter(),
        TrackerType.GitLab => new GitLabTicketRewriter(
            connections.GitLab(config), httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabTicketRewriter>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };

    /// <summary>
    /// 2026-09-27-5c1ea: the same connections, built for the one read a person drives by typing.
    /// GitHub's search takes the Octokit client as its interface so the request it builds is
    /// assertable; the others take the shared HttpClient as their siblings do.
    /// </summary>
    public ITicketSearch Search(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => new AzureDevOpsTicketSearch(
            connections.AzureDevOps(config), TrackerConnections.OpenStates(config),
            TrackerConnections.ExtraFields(config),
            loggerFactory.CreateLogger<AzureDevOpsTicketSearch>()),
        TrackerType.GitHub => new GitHubTicketSearch(
            connections.GitHubClient(config), connections.GitHub(config),
            loggerFactory.CreateLogger<GitHubTicketSearch>()),
        TrackerType.Jira => new JiraTicketSearch(
            connections.Jira(config), httpClientFactory.CreateClient(), new JiraFieldMapper(),
            loggerFactory.CreateLogger<JiraTicketSearch>()),
        TrackerType.GitLab => new GitLabTicketSearch(
            connections.GitLab(config), httpClientFactory.CreateClient(), new GitLabFieldMapper(),
            loggerFactory.CreateLogger<GitLabTicketSearch>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };

    /// <summary>
    /// 2026-09-28-1da5c: what the tracker LINKS to a ticket. Only Azure DevOps links both branches
    /// and pull requests; GitLab links merge requests and no branches; GitHub reaches pull requests
    /// through an issue's timeline and its linked branches only through an API this tree does not
    /// speak; Jira keeps both behind an integration and refuses with that reason.
    /// </summary>
    public ITicketLinkedWork LinkedWork(TrackerConnection config) => config.Type switch
    {
        TrackerType.AzureDevOps => new AzureDevOpsLinkedWork(
            connections.AzureDevOps(config), loggerFactory.CreateLogger<AzureDevOpsLinkedWork>()),
        TrackerType.GitHub => new GitHubLinkedWork(
            connections.GitHubClient(config), connections.GitHub(config),
            loggerFactory.CreateLogger<GitHubLinkedWork>()),
        TrackerType.Jira => new JiraLinkedWork(),
        TrackerType.GitLab => new GitLabLinkedWork(
            connections.GitLab(config), httpClientFactory.CreateClient(),
            loggerFactory.CreateLogger<GitLabLinkedWork>()),
        _ => throw new ConfigurationException($"Unknown ticket provider type: {config.Type}"),
    };
}
