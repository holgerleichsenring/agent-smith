namespace AgentSmith.Contracts.Providers;

/// <summary>
/// GitLab source-provider credentials. ProjectPath is URL-escaped at the
/// factory boundary so consumers can pass it directly into REST paths.
/// </summary>
public sealed record GitLabSourceConnection(
    string BaseUrl,
    string ProjectPath,
    string CloneUrl,
    string PrivateToken,
    string? DefaultBranch = null);
