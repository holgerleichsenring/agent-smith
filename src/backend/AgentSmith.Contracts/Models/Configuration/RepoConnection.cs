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

    /// <summary>
    /// 2026-09-01-1335: whether this connection names a repository at all — a remote by
    /// <see cref="Url"/>, or a working copy on this machine by <see cref="Path"/>. Context
    /// discovery reads through the source provider either way, so "has no url" was never
    /// the same question as "has nothing to read", and answering it that way cost a local
    /// run every image and every gate its own context.yaml declares.
    /// </summary>
    public bool HasLocation => !string.IsNullOrEmpty(Url) || !string.IsNullOrEmpty(Path);
}
