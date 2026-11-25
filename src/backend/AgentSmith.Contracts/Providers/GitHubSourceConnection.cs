namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitHub source-provider credentials. Used by GitHubSourceProvider and
/// GitHubPrDiffProvider to address a single repository.
/// </summary>
/// <param name="SelfLogin">2026-11-25-9992h: the login the token acts as when <c>GET /user</c> cannot say it — a GitHub App's
/// <c>{slug}[bot]</c>, read on first use; null for a user token.</param>
public sealed record GitHubSourceConnection(
    string RepoUrl,
    string Token,
    string? DefaultBranch = null,
    Func<string?>? SelfLogin = null);
