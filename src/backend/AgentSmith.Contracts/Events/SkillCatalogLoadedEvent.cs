namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173c: emitted by <c>YamlSkillLoader</c> after a catalog load
/// completes. Carries the headline counts; per-drop detail (which rule
/// failed for which SKILL.md) flows through p0169j-b1's
/// <c>CatalogIssueEvent</c> on the run channel — the two events are
/// complementary, not duplicate.
/// </summary>
public sealed record SkillCatalogLoadedEvent(
    string Source,
    string CatalogVersion,
    int SkillsLoaded,
    int SkillsDropped,
    long DurationMs,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.SkillCatalogLoaded, Timestamp);
