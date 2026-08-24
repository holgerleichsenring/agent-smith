using AgentSmith.Contracts.Models;
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
    private string _origin = "(catalog not resolved)";

    public string Root => _root
        ?? throw new InvalidOperationException(
            "Skill catalog has not been resolved yet — bootstrap service must run before SkillLoader.");

    // p0504: never throws — a refusal message must be able to name the catalog even
    // when the catalog is the thing that is missing.
    public string Origin => _origin;

    internal void Set(CatalogResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        _root = resolution.Root;
        // p0514: an overlaid root is not the pinned catalog, so the phrase that
        // names the catalog says so rather than reporting the base version alone.
        var overlay = resolution.Overlay is null ? string.Empty : $" + overlay {resolution.Overlay}";
        _origin = $"{resolution.Source.ToString().ToLowerInvariant()} {resolution.Version}{overlay} "
            + $"at {resolution.Root}";
    }
}
