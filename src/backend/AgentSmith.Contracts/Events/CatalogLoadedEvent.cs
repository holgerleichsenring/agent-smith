namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0205: per-run event emitted by the visible LoadCatalog step. Records the
/// binding THIS run resolved the skill catalog to — the real version (release
/// tag), the source mode + origin URL, the catalog counts (concepts / skills /
/// masters), whether the warm cache was re-used, and how long resolution took.
/// Distinct from the system-scoped <see cref="SkillCatalogLoadedEvent"/>: the
/// run-detail page must show what THIS run bound to, while the system page event
/// stays a process-wide signal.
///
/// p0210: the SkillNames / MasterNames / ConceptNames inventories carry the
/// actual catalog contents this run bound to, sorted alphabetically server-side,
/// so the run-detail body can list the names — not just the counts.
/// </summary>
public sealed record CatalogLoadedEvent(
    string RunId,
    string Version,
    string Source,
    string SourceUrl,
    int ConceptCount,
    int SkillsLoaded,
    int MastersCount,
    bool FromCache,
    long DurationMs,
    DateTimeOffset Timestamp,
    IReadOnlyList<string> SkillNames,
    IReadOnlyList<string> MasterNames,
    IReadOnlyList<string> ConceptNames)
    : RunEvent(RunId, EventType.CatalogLoaded, Timestamp);
