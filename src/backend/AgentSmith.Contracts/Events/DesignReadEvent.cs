namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-01-7f7ae: one design_read answered by Figma — which source, file and node the master
/// read and the file version and last modification the response carried. The run records what
/// it read; the version a ticket cites travels in the ticket text, so a later reader can tell
/// whether the design the run built from is the design the ticket meant.
/// </summary>
public sealed record DesignReadEvent(
    string RunId,
    string Source,
    string FileKey,
    string NodeId,
    string Version,
    string LastModified,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.DesignRead, Timestamp);
