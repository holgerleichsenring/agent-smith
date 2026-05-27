namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Jira ticket-provider credentials. Shared by JiraTicketProvider,
/// JiraTicketStatusTransitioner, and the internal JiraIssueSearcher /
/// JiraTransitioner. ProjectKey is optional for label-mode (no project
/// scoping).
/// </summary>
public sealed record JiraTicketConnection(
    string BaseUrl,
    string Email,
    string ApiToken,
    string? ProjectKey = null);
