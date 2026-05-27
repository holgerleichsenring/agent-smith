namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Azure Repos source-provider credentials. OrganizationUrl is the bare
/// org URL (https://dev.azure.com/{org}), Project and RepoName are
/// already-parsed segments.
/// </summary>
public sealed record AzureReposSourceConnection(
    string OrganizationUrl,
    string Project,
    string RepoName,
    string PersonalAccessToken);
