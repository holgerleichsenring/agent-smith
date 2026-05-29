namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Azure Repos source-provider credentials. OrganizationUrl is the bare
/// org URL (https://dev.azure.com/{org}), Project and RepoName are
/// already-parsed segments. DefaultBranch overrides the Azure DevOps
/// API-reported default when set in the catalog entry — same shape as
/// GitHub and GitLab connection records.
/// </summary>
public sealed record AzureReposSourceConnection(
    string OrganizationUrl,
    string Project,
    string RepoName,
    string PersonalAccessToken,
    string? DefaultBranch = null);
