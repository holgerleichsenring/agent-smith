namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Azure DevOps ticket-provider credentials. Shared by
/// AzureDevOpsTicketProvider, AzureDevOpsTicketStatusTransitioner,
/// AzureDevOpsAttachmentLoader, and AzureDevOpsConnectionCache.
/// </summary>
public sealed record AzureDevOpsTicketConnection(
    string OrganizationUrl,
    string Project,
    string PersonalAccessToken,
    Tickets.TicketLabelVocabulary? Labels = null)
{
    /// <summary>2026-09-25-3c7ac: what this board calls the labels the framework writes. Null is
    /// today's vocabulary, which is what a tracker configuring nothing gets.</summary>
    public Tickets.TicketLabelVocabulary ResolvedLabels => Labels ?? Tickets.TicketLabelVocabulary.Default;
}
