using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves the on-disk skill catalog directory from a <see cref="SkillsConfig"/>
/// in a specific source mode (default / path / url). Implementations are
/// dispatched by <see cref="SkillsConfig.Source"/>.
/// </summary>
public interface ISkillsSourceHandler
{
    /// <summary>Source mode this handler implements.</summary>
    SkillsSourceMode Mode { get; }

    /// <summary>
    /// Materialises the catalog and returns the resolved binding: the absolute
    /// path to the directory containing the <c>skills/</c> subtree, the real
    /// version, the origin URL, and whether this call re-used the warm cache.
    /// Throws on misconfiguration or unrecoverable download/extract failure.
    /// </summary>
    Task<CatalogResolution> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken);
}
