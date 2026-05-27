namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Source repository connection, materialized from a named catalog entry.
/// Name is the catalog key; Type=Local uses Path, remote types use Url.
/// </summary>
public sealed record RepoConnection
{
    public string Name { get; init; } = string.Empty;
    public RepoType Type { get; init; } = RepoType.GitHub;
    public string? Url { get; init; }
    public string? Path { get; init; }
    public string? Organization { get; init; }
    public string? Project { get; init; }
    public string Auth { get; init; } = string.Empty;
    public string? DefaultBranch { get; init; }
}
