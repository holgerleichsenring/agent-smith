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

    /// <summary>
    /// 2026-08-30-c6ec: the served interface this repository CONSUMES, named as the
    /// interface's served description titles it. It is what tells a run which checkouts
    /// hold first-party call sites — the intent an interface is used with lives in its
    /// clients, not in the server. A name that resolves against nothing fails the run.
    /// </summary>
    public string? Consumes { get; init; }
}
