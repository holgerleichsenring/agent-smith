namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitLab ticket-provider credentials. Shared by GitLabTicketProvider,
/// GitLabTicketStatusTransitioner, GitLabAttachmentLoader, and the
/// internal GitLabIssueLister. ProjectPath is URL-escaped.
/// </summary>
public sealed record GitLabTicketConnection(
    string BaseUrl,
    string ProjectPath,
    string PrivateToken);
