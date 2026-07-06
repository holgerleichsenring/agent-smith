namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0316: the coding master refused a ticket-embedded instruction — either
/// out-of-scope/destructive (the never-comply catalog) or a prompt-injection
/// attempt. Emitted once per ignored instruction so the dashboard + audit trail
/// see verbatim what was refused and why; the same data is persisted in result.md.
/// </summary>
public sealed record TicketInstructionIgnoredEvent(
    string RunId,
    string Quote,
    string Reason,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.TicketInstructionIgnored, Timestamp);
