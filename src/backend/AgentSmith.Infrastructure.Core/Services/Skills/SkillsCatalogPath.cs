using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Mutable holder for the resolved catalog path. Populated by
/// <c>SkillsBootstrapHostedService</c>; consumed by skill loaders after boot.
/// Registered as Singleton.
/// </summary>
public sealed class SkillsCatalogPath : ISkillsCatalogPath, IResolvedCatalogBinding
{
    private const string Unresolved = "(catalog not resolved)";

    private CatalogResolution? _resolution;

    public string Root => _resolution?.Root
        ?? throw new InvalidOperationException(
            "Skill catalog has not been resolved yet — bootstrap service must run before SkillLoader.");

    // p0504: never throws — a refusal message must be able to name the catalog even
    // when the catalog is the thing that is missing.
    public string Origin => _resolution?.Origin ?? Unresolved;

    // 2026-09-20-4981: the whole binding, for a reader that needs its parts rather than
    // the phrase — the anonymous installation report states source, version and overlay
    // and deliberately drops the root.
    public CatalogResolution? Current => _resolution;

    internal void Set(CatalogResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        // p0514: an overlaid root is not the pinned catalog, so the phrase that names the
        // catalog says so rather than reporting the base version alone.
        // 2026-09-18-84be: the phrase is minted by the resolution — a caller that holds one
        // reads the same sentence this singleton publishes.
        _resolution = resolution;
    }
}
