using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Jira ticket-provider credentials. Shared by JiraTicketProvider,
/// JiraTicketStatusTransitioner, and the internal JiraIssueSearcher /
/// JiraTransitioner. ProjectKey scopes every read and is where a ticket is filed;
/// the tracker requires it (2026-10-27-a4e3), and a configuration stored without
/// one still reads across every project the account sees. Endpoints carries the operator-overridable REST paths; when null
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
    Tickets.TicketLabelVocabulary? Labels = null)
{
    private static readonly JiraEndpoints DefaultEndpoints = new();

    /// <summary>Endpoint templates with Jira-Cloud-v3 defaults applied when unset.</summary>
    public JiraEndpoints ResolvedEndpoints => Endpoints ?? DefaultEndpoints;

    /// <summary>Native-status map, or <see cref="JiraLifecycleStatusMap.Empty"/> when unset (label mode).</summary>
    public JiraLifecycleStatusMap ResolvedLifecycleMap => LifecycleStatusMap ?? JiraLifecycleStatusMap.Empty;

    /// <summary>2026-09-25-3c7ac: what this board calls the labels the framework writes. Null is
    /// today's vocabulary, which is what a tracker configuring nothing gets.</summary>
    public Tickets.TicketLabelVocabulary ResolvedLabels => Labels ?? Tickets.TicketLabelVocabulary.Default;

}
