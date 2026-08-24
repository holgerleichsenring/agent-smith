using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0514: layers an operator-supplied overlay directory ON TOP OF the catalog the
/// configured source handler resolved, instead of replacing it. Orthogonal to how
/// the base arrived — a pinned release, the embedded catalog and a mounted
/// directory all take the same overlay.
/// </summary>
public interface ISkillsOverlayMaterializer
{
    /// <summary>
    /// Returns <paramref name="baseResolution"/> unchanged when no overlay is
    /// configured. Otherwise materialises base and overlay into one root by
    /// file-level union — the overlay wins per file, the base survives everywhere
    /// else — and returns the binding pointing at that root. Throws when the
    /// configured overlay is missing or is not a catalog directory: an overlay
    /// that silently resolved to the bare base would ship runs without the
    /// operator's own skills and say nothing.
    /// </summary>
    CatalogResolution Apply(CatalogResolution baseResolution, SkillsConfig config);
}
