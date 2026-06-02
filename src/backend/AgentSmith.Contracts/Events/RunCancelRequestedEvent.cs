namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0200: published when an operator (via POST /api/runs/{runId}/cancel) or
/// the PipelineRunWatchdog signals a cancel. <see cref="Reason"/> distinguishes
/// the two paths so the dashboard can label "cancelled by operator" vs.
/// "cancelled by watchdog (wall-time exceeded)". Run terminates with the
/// usual RunFinished(status=failed, summary="cancelled") once the executor
/// returns.
/// </summary>
public sealed record RunCancelRequestedEvent(
    string RunId,
    string Reason,
    DateTimeOffset RequestedAt)
    : RunEvent(RunId, EventType.RunCancelRequested, RequestedAt);
