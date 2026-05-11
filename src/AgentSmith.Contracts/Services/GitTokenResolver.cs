namespace AgentSmith.Contracts.Services;

/// <summary>
/// Maps a source-provider <c>Type</c> (GitHub / GitLab / AzureRepos) to the
/// environment-variable name that holds the operator's PAT for that platform,
/// and reads the value from the host environment.
///
/// The git credential helper used by clone Steps echoes <c>$GIT_TOKEN</c> as
/// the password (see CheckoutSourceHandler / HostSourceCloner / SandboxGitOperations);
/// the resolved value here is what the caller stamps into <c>Step.Env["GIT_TOKEN"]</c>
/// or the spawned process environment so the helper has something to expand.
///
/// Returns <c>null</c> when the source type is unrecognized OR the env var
/// isn't set; callers decide whether that's a soft-skip (HostSourceCloner)
/// or a hard fail later in the clone (CheckoutSourceHandler).
/// </summary>
public static class GitTokenResolver
{
    public static string? Resolve(string sourceType) => sourceType switch
    {
        var t when t.Equals("GitHub", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
        var t when t.Equals("GitLab", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("GITLAB_TOKEN"),
        var t when t.Equals("AzureRepos", StringComparison.OrdinalIgnoreCase)
            => Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN"),
        _ => null,
    };
}
