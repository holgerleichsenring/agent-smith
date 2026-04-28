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
    /// Local cache directory where downloaded tarballs are extracted. Defaults to
    /// <c>/var/lib/agentsmith/skills</c>.
    /// </summary>
    public string CacheDir { get; set; } = "/var/lib/agentsmith/skills";
}
