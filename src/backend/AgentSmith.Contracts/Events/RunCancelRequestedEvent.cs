namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0200: published when an operator (via POST /api/runs/{runId}/cancel) or
/// the PipelineRunWatchdog signals a cancel. <see cref="Reason"/> distinguishes
/// the two paths so the dashboard can label "cancelled by operator" vs.
/// "cancelled by watchdog (wall-time exceeded)". p0259: the run terminates with
/// RunFinished(status=cancelled) for an operator/watchdog cancel (a vanished
/// sandbox stays status=failed). p0259 also persists this event onto the run row
/// (RunEventApplier) so the "cancelling…" state survives a navigation/reload.
/// </summary>
public sealed record RunCancelRequestedEvent(
    string RunId,
    string Reason,
    DateTimeOffset RequestedAt)
    : RunEvent(RunId, EventType.RunCancelRequested, RequestedAt);
