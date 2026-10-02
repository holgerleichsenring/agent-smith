using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: which <see cref="LegacyCredentialKey"/> a repo, connection or tracker type
/// read before it read its own auth — the names the shipped example catalog maps to the fixed
/// variables. A local repo has none; it authenticates with nothing.
/// </summary>
public static class LegacyCredentialKeys
{
    public static LegacyCredentialKey? For(RepoType type) => type switch
    {
        RepoType.GitHub => new("github_token", "GITHUB_TOKEN"),
        RepoType.GitLab => new("gitlab_token", "GITLAB_TOKEN"),
        RepoType.AzureDevOps => new("azure_devops_token", "AZURE_DEVOPS_TOKEN"),
        _ => null,
    };

    public static LegacyCredentialKey For(TrackerType type) => type switch
    {
        TrackerType.GitHub => For(RepoType.GitHub)!,
        TrackerType.GitLab => For(RepoType.GitLab)!,
        TrackerType.AzureDevOps => For(RepoType.AzureDevOps)!,
        _ => new("jira_token", "JIRA_TOKEN"),
    };
}
