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
    JiraEndpoints? Endpoints = null,
    JiraLifecycleStatusMap? LifecycleStatusMap = null,
    string? ParentLinkType = null)
{
    private static readonly JiraEndpoints DefaultEndpoints = new();

    /// <summary>The issue link type a filed child is linked to its parent with. A Task cannot
    /// parent a Task, so the link is a plain issue link; a site may rename its types.</summary>
    public const string DefaultParentLinkType = "Relates";

    /// <summary>Endpoint templates with Jira-Cloud-v3 defaults applied when unset.</summary>
    public JiraEndpoints ResolvedEndpoints => Endpoints ?? DefaultEndpoints;

    /// <summary>Native-status map, or <see cref="JiraLifecycleStatusMap.Empty"/> when unset (label mode).</summary>
    public JiraLifecycleStatusMap ResolvedLifecycleMap => LifecycleStatusMap ?? JiraLifecycleStatusMap.Empty;
}
