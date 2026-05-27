namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitHub ticket-provider credentials. Shared by GitHubTicketProvider,
/// GitHubTicketStatusTransitioner, and the internal GitHubIssueLister.
/// </summary>
public sealed record GitHubTicketConnection(
    string RepoUrl,
    string Token);
