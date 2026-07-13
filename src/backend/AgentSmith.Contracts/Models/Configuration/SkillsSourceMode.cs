namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// How the server resolves the skill catalog at boot.
/// </summary>
public enum SkillsSourceMode
{
    /// <summary>Pull the official release from the agentsmith-skills repo.</summary>
    Default,

    /// <summary>Use a pre-mounted directory (operator-managed, e.g. K8s volume).</summary>
    Path,

    /// <summary>Pull from an explicit URL with optional SHA256 verification.</summary>
    Url,

    /// <summary>
    /// p0325: use the skills release embedded in the binary at build time.
    /// The effective default when no explicit skills source is configured —
    /// no network, no pin, the release always carries the catalog it was
    /// tested with.
    /// </summary>
    Embedded,
}
