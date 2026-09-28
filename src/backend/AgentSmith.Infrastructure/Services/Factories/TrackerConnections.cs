using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// 2026-09-27-5c1ea: builds the per-tracker connection record from a configured tracker and the
/// secret store. Extracted from <see cref="TicketProviderFactory"/> once a THIRD capability wanted
/// the same four records — the provider, the rewriter and the search were each re-deriving the
/// same URLs, tokens and escaping, and a fourth would have been the fourth copy.
/// </summary>
internal sealed class TrackerConnections(SecretsProvider secrets)
{
    public AzureDevOpsTicketConnection AzureDevOps(TrackerConnection config) => new(
        $"https://dev.azure.com/{config.Organization}", config.Project!,
        secrets.GetRequired("AZURE_DEVOPS_TOKEN"), TicketLabelVocabulary.For(config));

    public GitHubTicketConnection GitHub(TrackerConnection config) => new(
        config.Url!, secrets.GetRequired("GITHUB_TOKEN"), TicketLabelVocabulary.For(config));

    public JiraTicketConnection Jira(TrackerConnection config) => new(
        config.Url ?? secrets.GetRequired("JIRA_URL"), secrets.GetRequired("JIRA_EMAIL"),
        secrets.GetRequired("JIRA_TOKEN"), config.Project, config.Endpoints,
        Labels: TicketLabelVocabulary.For(config));

    public GitLabTicketConnection GitLab(TrackerConnection config) => new(
        secrets.GetOptional("GITLAB_URL") ?? AgentDefaults.DefaultGitLabBaseUrl,
        // The project path is a path and travels inside a URL segment.
        Uri.EscapeDataString(config.Project ?? secrets.GetRequired("GITLAB_PROJECT")),
        secrets.GetRequired("GITLAB_TOKEN"), TicketLabelVocabulary.For(config));

    /// <summary>
    /// The Octokit client for a configured repository. The provider builds its own inside its
    /// constructor; the search takes one so the request it composes can be asserted.
    /// </summary>
    public IGitHubClient GitHubClient(TrackerConnection config) =>
        new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(secrets.GetRequired("GITHUB_TOKEN")) };

    /// <summary>The states an operator calls open, or null when they left it to the default.</summary>
    public static IReadOnlyList<string>? OpenStates(TrackerConnection config) =>
        config.OpenStates.Count > 0 ? config.OpenStates : null;

    public static IReadOnlyList<string>? ExtraFields(TrackerConnection config) =>
        config.ExtraFields.Count > 0 ? config.ExtraFields : null;
}
