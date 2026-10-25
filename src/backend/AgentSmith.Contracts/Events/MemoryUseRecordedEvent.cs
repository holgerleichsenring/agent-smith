using AgentSmith.Contracts.Memory;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-25-9324b: what a master said about the memories it used — the memories a spec acted on
/// (<see cref="Relied"/>, the spec's whole list: it replaces the one before), or one memory that led it wrong
/// (<see cref="Disputed"/>, with <see cref="Reason"/>). A dispute outside a spec carries no spec id.
/// </summary>
public sealed record MemoryUseRecordedEvent(
    string RunId,
    DateTimeOffset Timestamp,
    string? SpecId,
    string Project,
    string TicketId,
    IReadOnlyList<MemoryCitation> Relied,
    MemoryCitation? Disputed,
    string? Reason)
    : RunEvent(RunId, EventType.MemoryUseRecorded, Timestamp);
