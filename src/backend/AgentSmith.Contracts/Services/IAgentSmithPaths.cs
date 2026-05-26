namespace AgentSmith.Contracts.Services;

/// <summary>
/// Central resolver for host-side filesystem locations agent-smith owns
/// outside any project's repo tree. All cache artifacts go through here so
/// they share a single, predictable layout (rooted at <c>$XDG_CACHE_HOME</c>
/// → <c>$HOME/.cache/agentsmith</c>) and never accidentally end up inside
/// a project's <c>.agentsmith/</c> directory where they would be committed
/// by <c>InitCommit</c> / <c>CommitAndPR</c>'s blanket <c>git add -A</c>.
/// </summary>
public interface IAgentSmithPaths
{
    /// <summary>
    /// Root of agent-smith's cache tree on the host. Concrete subdirectories
    /// (skill catalog, project-map cache, ...) live underneath. Created
    /// on demand by callers when they write into it.
    /// </summary>
    string CacheRoot { get; }

    /// <summary>
    /// Default skill-catalog extract location (<c>{CacheRoot}/skills</c>).
    /// <see cref="Models.Configuration.SkillsConfig.CacheDir"/> defaults to
    /// this; server deployments override it (e.g. <c>/var/lib/agentsmith/skills</c>)
    /// when the cache should live outside <c>$HOME</c>.
    /// </summary>
    string SkillsCatalogRoot { get; }

    /// <summary>
    /// Per-project cache directory, keyed by the repository's remote URL so
    /// two checkouts of the same repo share a cache entry across runs and
    /// machines stay isolated from each other. Caller is responsible for
    /// creating the directory before writing to it.
    /// </summary>
    string ProjectCacheDir(string repositoryRemoteUrl);
}
