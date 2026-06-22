using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Jira ticket-provider credentials. Shared by JiraTicketProvider,
/// JiraTicketStatusTransitioner, and the internal JiraIssueSearcher /
/// JiraTransitioner. ProjectKey is optional for label-mode (no project
/// scoping). Endpoints carries the operator-overridable REST paths; when null
/// (e.g. direct construction in tests) <see cref="ResolvedEndpoints"/> applies
/// the Jira-Cloud-v3 defaults.
/// </summary>
public sealed record JiraTicketConnection(
    string BaseUrl,
    string Email,
    string ApiToken,
    string? ProjectKey = null,
    JiraEndpoints? Endpoints = null)
{
    private static readonly JiraEndpoints DefaultEndpoints = new();

    /// <summary>Endpoint templates with Jira-Cloud-v3 defaults applied when unset.</summary>
    public JiraEndpoints ResolvedEndpoints => Endpoints ?? DefaultEndpoints;
}
