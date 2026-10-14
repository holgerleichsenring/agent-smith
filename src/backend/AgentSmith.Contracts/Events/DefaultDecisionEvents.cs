namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-8762o: a choice a master took by default — one no person had to make before work went on, but
/// one a person may ratify or overrule later. <see cref="Kind"/> is one of
/// <see cref="Decisions.DefaultDecisionKinds"/>: a <c>decision</c> carries the question, the choice, its
/// alternatives and why; a <c>memory-proposal</c> carries only the slug as <see cref="Chosen"/> and the
/// memory file as <see cref="PointerPath"/>, never its body. <see cref="HoldsSeries"/> flags the row for the
/// next automatic attempt only. <see cref="Conversation"/> is the design turn's conversation, null in a
/// coding run.
/// </summary>
public sealed record DefaultDecisionTakenEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string? SpecId,
    string Question,
    string Chosen,
    IReadOnlyList<string> Alternatives,
    string Why,
    bool HoldsSeries,
    string Kind,
    string? PointerPath,
    string? Conversation)
    : RunEvent(RunId, EventType.DefaultDecisionTaken, Timestamp);
