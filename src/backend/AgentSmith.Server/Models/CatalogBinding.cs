using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-20-4981: which skill catalog this installation is bound to, as the ANONYMOUS
/// installation report states it — the source mode, the version and the overlay
/// fingerprint, plus whether that is what the server RESOLVED or only what it was
/// configured to resolve.
/// <para>
/// No root. Three of the four source modes name a public release tag, but a mounted
/// catalog's root is an operator's own directory and this surface is read without signing
/// in. The catalog endpoint, which needs a catalog permission, carries the full phrase
/// including the root.
/// </para>
/// </summary>
/// <param name="Source">Source mode: <c>default</c>, <c>path</c>, <c>url</c> or <c>embedded</c>.</param>
/// <param name="Version">The resolved release tag, or the configured pin when resolution
/// failed; null when nothing was pinned.</param>
/// <param name="Overlay">Fingerprint of the materialized overlay, or null. A configured
/// overlay that never materialized has no fingerprint, and its configured value is a path
/// this surface does not carry.</param>
/// <param name="Resolved">True when the server bound this catalog; false when this is only
/// what was configured and resolution did not succeed.</param>
public sealed record CatalogBinding(string Source, string? Version, string? Overlay, bool Resolved)
{
    /// <summary>
    /// The binding the server resolved, or — when it resolved none — what it was configured
    /// to resolve, said as configuration rather than as fact. A catalog that will not
    /// resolve is exactly when this report is being read, so going silent is not an option.
    /// </summary>
    public static CatalogBinding From(CatalogResolution? resolved, SkillsConfig configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        return resolved is null
            ? new CatalogBinding(Mode(configured.Source), Stated(configured.Version), null, Resolved: false)
            : new CatalogBinding(Mode(resolved.Source), Stated(resolved.Version), resolved.Overlay, Resolved: true);
    }

    private static string Mode(SkillsSourceMode source) => source.ToString().ToLowerInvariant();

    private static string? Stated(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
