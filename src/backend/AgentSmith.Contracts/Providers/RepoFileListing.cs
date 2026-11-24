namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-c81ea: the files under one folder of a branch, at any depth.
/// </summary>
/// <param name="Files">Repo-relative file paths (no folders), at most the bound the caller asked for.</param>
/// <param name="Truncated">True when the host cut its answer or more files exist than the caller's bound.</param>
public sealed record RepoFileListing(IReadOnlyList<string> Files, bool Truncated)
{
    public static RepoFileListing Empty { get; } = new([], false);

    /// <summary>At most <paramref name="maxFiles"/> of <paramref name="files"/>; truncated when more exist or the host cut.</summary>
    public static RepoFileListing Bounded(IReadOnlyList<string> files, int maxFiles, bool hostTruncated) =>
        files.Count > maxFiles
            ? new(files.Take(maxFiles).ToList(), true)
            : new(files, hostTruncated);
}
