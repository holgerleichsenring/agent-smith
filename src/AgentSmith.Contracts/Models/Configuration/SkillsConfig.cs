namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Skill catalog source configuration. Pure data — the YAML loader fills
/// <see cref="CacheDir"/> from <c>IAgentSmithPaths.SkillsCatalogRoot</c>
/// after deserialization when the operator hasn't set it explicitly. Direct
/// callers that bypass the loader (e.g. test fixtures) must populate CacheDir
/// themselves.
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
    /// Local cache directory where downloaded tarballs are extracted. Empty
    /// string means "use the IAgentSmithPaths default" — the YAML loader fills
    /// this from <c>SkillsCatalogRoot</c> when the operator didn't set it.
    /// Server deployments override this explicitly to a system-managed path
    /// like <c>/var/lib/agentsmith/skills</c> with a matching volume mount.
    /// </summary>
    public string CacheDir { get; set; } = string.Empty;
}
