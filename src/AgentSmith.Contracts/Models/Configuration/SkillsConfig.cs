namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Skill catalog source configuration. Determines how the server obtains
/// the skill files at boot.
/// </summary>
public sealed class SkillsConfig
{
    /// <summary>How to resolve the catalog. Default: <see cref="SkillsSourceMode.Default"/>.</summary>
    public SkillsSourceMode Source { get; set; } = SkillsSourceMode.Default;

    /// <summary>
    /// Release tag to pull when <see cref="Source"/> is <see cref="SkillsSourceMode.Default"/>.
    /// Required for the default source.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Pre-mounted directory containing the catalog when <see cref="Source"/> is
    /// <see cref="SkillsSourceMode.Path"/>. Must contain a <c>skills/</c> subdirectory.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Explicit tarball URL when <see cref="Source"/> is <see cref="SkillsSourceMode.Url"/>.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional SHA256 hash for tarball verification (any source mode that downloads).
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Local cache directory where downloaded tarballs are extracted. The default
    /// resolves a per-user path that works without elevated privileges, falling
    /// back to the OS temp directory when no HOME is set (e.g. minimal containers):
    ///
    /// 1. <c>$XDG_CACHE_HOME/agentsmith/skills</c>
    /// 2. <c>$HOME/.cache/agentsmith/skills</c> (or <c>$USERPROFILE\.cache\...</c> on Windows)
    /// 3. <c>{tmp}/agentsmith/skills</c>
    ///
    /// Server deployments override this explicitly to a system-managed path
    /// like <c>/var/lib/agentsmith/skills</c> with a matching volume mount.
    /// </summary>
    public string CacheDir { get; set; } = ResolveDefaultCacheDir();

    public static string ResolveDefaultCacheDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return System.IO.Path.Combine(xdg, "agentsmith", "skills");

        var home = Environment.GetEnvironmentVariable("HOME")
                ?? Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(home))
            return System.IO.Path.Combine(home, ".cache", "agentsmith", "skills");

        return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agentsmith", "skills");
    }
}
