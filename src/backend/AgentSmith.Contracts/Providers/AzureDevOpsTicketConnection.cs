namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Azure DevOps ticket-provider credentials. Shared by
/// AzureDevOpsTicketProvider, AzureDevOpsTicketStatusTransitioner,
/// AzureDevOpsAttachmentLoader, and AzureDevOpsConnectionCache.
/// </summary>
public sealed record AzureDevOpsTicketConnection(
    string OrganizationUrl,
    string Project,
    string PersonalAccessToken);
