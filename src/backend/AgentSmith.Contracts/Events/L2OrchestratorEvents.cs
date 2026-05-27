namespace AgentSmith.Contracts.Events;

public sealed record DecisionLoggedEvent(
    string RunId,
    string Category,
    string Chose,
    string? Over,
    string Reason,
    DateTimeOffset Timestamp)
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
