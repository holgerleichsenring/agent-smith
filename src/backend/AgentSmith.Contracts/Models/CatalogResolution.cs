using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0205: the binding a single run resolved the skill catalog to. Returned by
/// the source handler and the resolver so the visible Load-catalog step can
/// record exactly what THIS run bound to — the real version (release tag, not a
/// directory name), the source mode, the origin URL, and whether this call hit
/// the warm on-disk cache or triggered a fresh pull. Counts (concepts/skills/
/// masters) are not part of the binding; they are gathered by the loader at
/// step time.
/// </summary>
public sealed record CatalogResolution(
    string Root,
    string Version,
    SkillsSourceMode Source,
    string SourceUrl,
    bool FromCache);
