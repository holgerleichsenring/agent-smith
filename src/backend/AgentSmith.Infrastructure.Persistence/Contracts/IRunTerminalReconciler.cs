using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// p0378: repairs a run whose Redis stream carries a terminal RunFinished the
/// drain never fully persisted. The row is finalized from the stream's own
/// terminal event (stream-authoritative) and the RunFinished trail row is
/// appended when missing. Idempotent — a fully persisted run is left untouched.
/// </summary>
public interface IRunTerminalReconciler
{
    /// <summary>2026-10-26-0f19a: <paramref name="position"/> is the terminal entry's place in its run stream.</summary>
    Task ReconcileAsync(RunFinishedEvent terminal, long position, CancellationToken cancellationToken);
}
