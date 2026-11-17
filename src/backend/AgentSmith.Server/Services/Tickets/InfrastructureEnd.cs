using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the ONE rule for "this attempt ended on infrastructure, not on its work or a
/// person": it was cancelled because no process was driving it any more (interrupted — the
/// liveness reaper and the queued-run sweeper) or its owning replica went away (stale-lease-reaped).
/// A host that stopped beating IS interrupted, and capacity never ends an attempt — a rejection
/// keeps the row queued — so neither needs an arm of its own.
/// <para>2026-10-13-9a59n: or a failed or cancelled run lost a sandbox in its LAST launch — vanished or
/// OOM-killed, placed by trail position inside that segment. A delivered run with an OOM'd sandbox did not end
/// on it, a sandbox of an earlier launch was relaunched past, and a row without a position cannot be placed.
/// 'gone' is not an arm: it is what the release read when the sandbox was already gone or its exit could not be
/// read, after the run had ended on its own terms.</para>
/// <para>2026-11-16-f3fba: or it failed because the host could not say whether its repositories carry their
/// bootstrap (readiness-unknown) — the repositories were never judged, so the work did not end it.</para>
/// </summary>
public sealed class InfrastructureEnd
{
    /// <summary>The reason StaleLeaseRelease cancels with.</summary>
    public const string StaleLeaseReaped = OwedWorkCancel.StaleLeaseReaped;

    /// <param name="lastLaunch">The run's last segment and its sandbox ends; null reads the sandbox arm false.</param>
    public bool Holds(Run run, RunLastLaunch? lastLaunch = null) =>
        (run.Status == RunStatuses.Failed && run.EndKind == RunEndKinds.ReadinessUnknown)
        || (run.Status == TicketAttempts.Cancelled
            && run.CancelReason is RunLivenessReaper.InterruptedReason or StaleLeaseReaped)
        || (run.Status is RunStatuses.Failed or TicketAttempts.Cancelled
            && lastLaunch is not null && lastLaunch.SandboxEnds.Any(s => InLaunch(s, lastLaunch.Segment)));

    private static bool InLaunch(RunSandbox sandbox, RunSegment segment) =>
        sandbox.EndReason is SandboxEndReasons.Vanished or SandboxEndReasons.OomKilled
        && sandbox.EventSeq is { } at && segment.EventSeq is { } start && start < at
        && (segment.EndSeq is null || at < segment.EndSeq);
}
