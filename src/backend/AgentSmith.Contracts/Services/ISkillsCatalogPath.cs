namespace AgentSmith.Contracts.Services;

/// <summary>
/// Exposes the resolved skill-catalog directory once the bootstrap service has
/// pulled or validated it. Consumed by <c>SkillLoader</c> after boot.
/// </summary>
public interface ISkillsCatalogPath
{
    /// <summary>Absolute path to the directory containing the <c>skills/</c> subtree.</summary>
    string Root { get; }

    /// <summary>
    /// p0504: what this catalog IS, in one operator-checkable phrase — source mode,
    /// version and root, e.g. <c>embedded v4.6.0 at /var/cache/agentsmith/skills</c>.
    /// With four source modes, "the pin" names nothing an operator can go and look at,
    /// so a refusal that blames the catalog has to say which catalog.
    /// </summary>
    string Origin { get; }
}
