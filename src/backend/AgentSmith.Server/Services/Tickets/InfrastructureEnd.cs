using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ONE rule for "this attempt ended on infrastructure, not on its work or a
/// person": it was cancelled because no process was driving it any more (interrupted — the
/// liveness reaper and the queued-run sweeper) or its owning replica went away (stale-lease-reaped).
/// A host that stopped beating IS interrupted, and capacity never ends an attempt — a rejection
/// keeps the row queued — so neither needs an arm of its own.
/// </summary>
public sealed class InfrastructureEnd
{
    /// <summary>The reason StaleLeaseRelease cancels with.</summary>
    public const string StaleLeaseReaped = "stale-lease-reaped";

    public bool Holds(Run run) =>
        run.Status == TicketAttempts.Cancelled
        && run.CancelReason is RunLivenessReaper.InterruptedReason or StaleLeaseReaped;
}
