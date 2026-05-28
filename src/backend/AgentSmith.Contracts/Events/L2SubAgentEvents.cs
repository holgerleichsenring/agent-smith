namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0177: emitted by <c>SubAgentRunner</c> just before the agentic loop
/// for one child starts. Carries the child's identity tuple
/// (<see cref="Name"/>, <see cref="Activity"/>, <see cref="SubAgentId"/>,
/// optional <see cref="ParentSubAgentId"/> when the master is itself a
/// sub-agent — currently always null per the one-level topology) so the
/// dashboard can render the child node and the master->child edge.
/// <see cref="InheritedContextHash"/> is a stable hash over the data
/// snapshot passed into the child; surface-level diagnostic only,
/// not a load-bearing identifier.
/// </summary>
public sealed record SubAgentSpawnedEvent(
    string RunId,
    string SubAgentId,
    string Name,
    string Activity,
    string? ParentSubAgentId,
    string InheritedContextHash,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentSpawned, Timestamp);

/// <summary>
/// p0177: typed observation row a sub-agent emits during its loop.
/// <see cref="Text"/> is the operator-readable body; the dashboard's
/// SubAgentTimelinePanel renders one row per observation with the
/// SubAgentId filter applied client-side.
/// </summary>
public sealed record SubAgentObservationEvent(
    string RunId,
    string SubAgentId,
    string Text,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentObservation, Timestamp);

/// <summary>
/// p0177: a sub-agent finding — anchor-grade observation with severity
/// + title + detail. Distinct from <see cref="SubAgentObservationEvent"/>
/// because the dashboard renders findings with a severity badge and
/// pins them at the top of the timeline.
/// </summary>
public sealed record SubAgentFindingEvent(
    string RunId,
    string SubAgentId,
    string Severity,
    string Title,
    string Detail,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentFinding, Timestamp);

/// <summary>
/// p0177: emitted when a sub-agent writes a file via FilesystemToolHost.
/// <see cref="Path"/> + <see cref="Bytes"/> are metadata only; file
/// content stays off the bus (same boundary as the L3 tool-result events).
/// </summary>
public sealed record SubAgentFileWrittenEvent(
    string RunId,
    string SubAgentId,
    string Path,
    long Bytes,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentFileWritten, Timestamp);

/// <summary>
/// p0177: emitted when a sub-agent invokes a tool through its scoped
/// ToolKit. Mirrors <see cref="ToolCallEvent"/> but attributes the call
/// to the child via <see cref="SubAgentId"/> so per-child activity
/// (and the sandbox-attribution view in p0173f) can be filtered.
/// </summary>
public sealed record SubAgentToolCallEvent(
    string RunId,
    string SubAgentId,
    string ToolName,
    string? ArgsSummary,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentToolCall, Timestamp);

/// <summary>
/// p0177: terminal event for one sub-agent. Decision-anchor counts plus
/// status + cost. The master's <c>SubAgentResult</c> is built from the
/// same counts so the LLM and the dashboard see the same truth.
/// </summary>
public sealed record SubAgentCompletedEvent(
    string RunId,
    string SubAgentId,
    string Status,
    int ObservationsCount,
    int FindingsCount,
    int FilesWrittenCount,
    int ToolCalls,
    decimal CostUsd,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.SubAgentCompleted, Timestamp);
