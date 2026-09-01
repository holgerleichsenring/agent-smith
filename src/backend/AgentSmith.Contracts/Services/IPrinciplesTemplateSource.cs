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
    /// Composes core + the delta for <paramref name="languageSlug"/>. Returns
    /// null when the resolved catalog does not ship the core template (older
    /// catalog pins) — callers then keep the pre-p0379 behavior.
    /// </summary>
    ComposedPrinciples? Compose(string languageSlug);
}
