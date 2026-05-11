namespace AgentSmith.Contracts.Services;

/// <summary>
/// Static cache-root resolver shared by <see cref="IAgentSmithPaths"/> (the
/// DI-friendly interface used by handlers) and
/// <c>SkillsConfig.ResolveDefaultCacheDir()</c> (a YAML-deserializer-triggered
/// property initializer that runs before DI exists). The logic must live in
/// exactly one place so the XDG resolution rules can't drift between the two
/// call sites.
/// </summary>
public static class DefaultPaths
{
    private const string AppName = "agentsmith";
    private const string SkillsSubdir = "skills";

    /// <summary>
    /// XDG-compliant cache root for agent-smith data:
    /// <c>$XDG_CACHE_HOME/agentsmith</c> → <c>$HOME/.cache/agentsmith</c> →
    /// <c>{tmp}/agentsmith</c> when no HOME is set (minimal containers).
    /// </summary>
    public static string ComputeCacheRoot()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, AppName);

        var home = Environment.GetEnvironmentVariable("HOME")
                ?? Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(home))
            return Path.Combine(home, ".cache", AppName);

        return Path.Combine(Path.GetTempPath(), AppName);
    }

    /// <summary>Default skill-catalog extract location: <c>{CacheRoot}/skills</c>.</summary>
    public static string ComputeSkillsCatalogRoot() =>
        Path.Combine(ComputeCacheRoot(), SkillsSubdir);
}
