namespace AgentSmith.Contracts.Services;

/// <summary>
/// Exposes the resolved skill-catalog directory once the bootstrap service has
/// pulled or validated it. Consumed by <c>SkillLoader</c> after boot.
/// </summary>
public interface ISkillsCatalogPath
{
    /// <summary>Absolute path to the directory containing the <c>skills/</c> subtree.</summary>
    string Root { get; }
}
