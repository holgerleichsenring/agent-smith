namespace AgentSmith.Contracts.Events;

/// <summary>
/// Catalog-loader issue surfaced into the event stream. Emitted when a YAML
/// skill or concept-vocabulary entry fails validation during run startup —
/// previously a silent stderr log that operators only discovered by reading
/// the container log. <see cref="Severity"/> uses "warning" for skip-and-continue
/// (skill validator drops one entry but loading proceeds) and "error" for
/// fatal startup failures (vocabulary parse failure that re-throws).
/// </summary>
public sealed record CatalogIssueEvent(
    string RunId,
    string Severity,
    string Source,
    string Category,
    string Message,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.CatalogIssue, Timestamp);
