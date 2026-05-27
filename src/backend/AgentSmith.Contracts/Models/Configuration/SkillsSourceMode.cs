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
}
