namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173c: emitted by <c>ConceptVocabularyLoader</c> on a successful
/// load. Failure path stays on p0169j-b1's <c>CatalogIssueEvent</c>
/// (run-scoped, error severity).
/// </summary>
public sealed record ConceptVocabularyLoadedEvent(
    string Source,
    int ConceptCount,
    long DurationMs,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.ConceptVocabularyLoaded, Timestamp);
