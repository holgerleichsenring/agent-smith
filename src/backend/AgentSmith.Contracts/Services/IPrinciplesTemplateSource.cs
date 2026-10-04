using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0379: provides the AUTHORED principles composition — universal core
/// plus per-language delta — from the resolved skill catalog. Principles are
/// authoritative gold, never inferred from a repo's code; composing for two
/// repos of the same stack yields byte-identical output.
/// </summary>
public interface IPrinciplesTemplateSource
{
    /// <summary>
    /// 2026-10-03-cf20c: the framework overlays the resolved catalog ships, each with the
    /// signals that apply it. Empty when the catalog carries none; a malformed overlay file is
    /// left out, because a wrongly applied overlay is a wrong mandate.
    /// </summary>
    IReadOnlyList<FrameworkOverlay> FrameworkOverlays();

    /// <summary>
    /// Composes core + the delta for <paramref name="languageSlug"/> + each overlay in
    /// <paramref name="overlaySlugs"/> (2026-10-03-cf20c; required, so no caller forgets
    /// detection by accident). Returns null when the resolved catalog does not ship the core
    /// template (older catalog pins) — callers then keep the pre-p0379 behavior.
    /// </summary>
    ComposedPrinciples? Compose(string languageSlug, IReadOnlyList<string> overlaySlugs);
}
