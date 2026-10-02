using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-09-27-5c1ea: builds the per-tracker connection record from a configured tracker and the
/// secret store. Extracted from <see cref="TicketProviderFactory"/> once a THIRD capability wanted
/// the same four records — the provider, the rewriter and the search were each re-deriving the
/// same URLs, tokens and escaping, and a fourth would have been the fourth copy.
/// 2026-10-02-5f89a: every value comes from the tracker itself — its own auth secret, url,
/// project and email; the legacy variables were written into it once, at load.
/// </summary>
internal sealed class TrackerConnections(ICredentialResolver credentials)
{
    public AzureDevOpsTicketConnection AzureDevOps(TrackerConnection config) => new(
        $"https://dev.azure.com/{config.Organization}", config.Project!,
        credentials.For(config), TicketLabelVocabulary.For(config));

    public GitHubTicketConnection GitHub(TrackerConnection config) => new(
        Required(config, config.Url, "url"), credentials.For(config), TicketLabelVocabulary.For(config));

    public JiraTicketConnection Jira(TrackerConnection config) => new(
        Required(config, config.Url, "url"), Required(config, config.Email, "email"),
        credentials.For(config), config.Project, config.Endpoints,
        Labels: TicketLabelVocabulary.For(config));

    public GitLabTicketConnection GitLab(TrackerConnection config) => new(
        string.IsNullOrWhiteSpace(config.Url) ? AgentDefaults.DefaultGitLabBaseUrl : config.Url.TrimEnd('/'),
        // The project path is a path and travels inside a URL segment.
        Uri.EscapeDataString(Required(config, config.Project, "project")),
        credentials.For(config), TicketLabelVocabulary.For(config));

    /// <summary>
    /// The Octokit client for a configured repository. The provider builds its own inside its
    /// constructor; the search takes one so the request it composes can be asserted.
    /// </summary>
    public IGitHubClient GitHubClient(TrackerConnection config) =>
        new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(credentials.For(config)) };

    /// <summary>The states an operator calls open, or null when they left it to the default.</summary>
    public static IReadOnlyList<string>? OpenStates(TrackerConnection config) =>
        config.OpenStates.Count > 0 ? config.OpenStates : null;

    public static IReadOnlyList<string>? ExtraFields(TrackerConnection config) =>
        config.ExtraFields.Count > 0 ? config.ExtraFields : null;

    private static string Required(TrackerConnection config, string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ConfigurationException($"Tracker '{config.Name}' ({config.Type}) declares no {field}.")
            : value;
}
