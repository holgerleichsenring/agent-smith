namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitHub source-provider credentials. Used by GitHubSourceProvider and
/// GitHubPrDiffProvider to address a single repository.
/// </summary>
public sealed record GitHubSourceConnection(
    string RepoUrl,
    string Token,
    string? DefaultBranch = null);
