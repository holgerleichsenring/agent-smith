namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0466: <see cref="DecisionLoggedEvent.SpecId"/> is the spec the decision was taken
/// in, read from the ambient step frame the runner opens. Null outside any spec and on
/// pre-p0466 payloads — a decision with no spec is unattributed, never guessed.
/// </summary>
public sealed record DecisionLoggedEvent(
    string RunId,
    string Category,
    string Chose,
    string? Over,
    string Reason,
    DateTimeOffset Timestamp,
    string? SpecId = null)
    : RunEvent(RunId, EventType.DecisionLogged, Timestamp);

public sealed record GateCheckedEvent(
    string RunId,
    string Gate,
    bool Passed,
    string Reason,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.GateChecked, Timestamp);

public sealed record TriageRouteEvent(
    string RunId,
    string Skill,
    string Role,
    int Confidence,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.TriageRoute, Timestamp);
