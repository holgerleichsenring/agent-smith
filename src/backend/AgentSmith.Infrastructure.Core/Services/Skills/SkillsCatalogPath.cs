using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Mutable holder for the resolved catalog path. Populated by
/// <c>SkillsBootstrapHostedService</c>; consumed by skill loaders after boot.
/// Registered as Singleton.
/// </summary>
public sealed class SkillsCatalogPath : ISkillsCatalogPath
{
    private string? _root;

    public string Root => _root
        ?? throw new InvalidOperationException(
            "Skill catalog has not been resolved yet — bootstrap service must run before SkillLoader.");

    internal void Set(string root) => _root = root;
}
