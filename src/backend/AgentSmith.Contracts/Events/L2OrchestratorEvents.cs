namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0466: <see cref="DecisionLoggedEvent.PhaseId"/> is the phase the decision was taken
/// in, read from the ambient step frame the runner opens. Null outside any phase and on
/// pre-p0466 payloads — a decision with no phase is unattributed, never guessed.
/// </summary>
public sealed record DecisionLoggedEvent(
    string RunId,
    string Category,
    string Chose,
    string? Over,
    string Reason,
    DateTimeOffset Timestamp,
    string? PhaseId = null)
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
