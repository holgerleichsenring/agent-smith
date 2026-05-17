using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Maps a repo's <see cref="RepoType"/> to the environment-variable name that
/// holds the operator's PAT for that platform, and reads the value from the
/// host environment.
///
/// The git credential helper used by clone Steps echoes <c>$GIT_TOKEN</c> as
/// the password (see CheckoutSourceHandler / HostSourceCloner / SandboxGitOperations);
/// the resolved value here is what the caller stamps into <c>Step.Env["GIT_TOKEN"]</c>
/// or the spawned process environment so the helper has something to expand.
///
/// Returns <c>null</c> when the env var isn't set or the type has no associated
/// remote PAT (Local); callers decide whether that's a soft-skip (HostSourceCloner)
/// or a hard fail later in the clone (CheckoutSourceHandler).
/// </summary>
public static class GitTokenResolver
{
    public static string? Resolve(RepoType type) => type switch
    {
        RepoType.GitHub => Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
        RepoType.GitLab => Environment.GetEnvironmentVariable("GITLAB_TOKEN"),
        RepoType.AzureDevOps => Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN"),
        _ => null,
    };
}
