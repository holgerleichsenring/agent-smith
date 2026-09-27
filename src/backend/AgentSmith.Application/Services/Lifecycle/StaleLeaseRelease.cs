using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-09-25-b4d9: what reaping ONE stale lease does — cancel the run, release the lease,
/// and hold the ticket against the reconciler while it happens. Extracted from
/// <see cref="ActiveRunReaper"/>, which owns the scan and was at its length ceiling.
/// <para>
/// The hold is the point: the reaper scans every sixty seconds and the reconciler every ten
/// minutes, and both act on a ticket whose lease went stale. Without the record's state to
/// separate them the reconciler could re-launch a ticket this class is mid-way through
/// cancelling — two loops over one ticket is how the label thrash of p0260 happened.
/// </para>
/// </summary>
public sealed class StaleLeaseRelease(
    IActiveRunLease lease,
    IRunCancellationRegistry cancellationRegistry,
    IEventPublisher eventPublisher,
    ITakenTicketStore takenTickets,
    TimeProvider timeProvider,
    ILogger<StaleLeaseRelease> logger)
{
    /// <summary>
    /// Reaps the candidate, or returns false when another pass already holds its ticket.
    /// </summary>
    public async Task<bool> ReapAsync(StaleLease candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!await takenTickets.TryBeginReapAsync(
                candidate.Project, candidate.TicketId.Value, cancellationToken))
        {
            logger.LogDebug(
                "Skipped stale lease {Project}/{Ticket}: another pass is already reaping it",
                candidate.Project, candidate.TicketId.Value);
            return false;
        }

        try
        {
            await CancelAndReleaseAsync(candidate, cancellationToken);
        }
        finally
        {
            // The lease is gone but the WORK is still owed, so the record stays and becomes
            // the reconciler's again. It is cleared by completion, never by this timer.
            await takenTickets.EndReapAsync(
                candidate.Project, candidate.TicketId.Value, CancellationToken.None);
        }
        return true;
    }

    private async Task CancelAndReleaseAsync(StaleLease candidate, CancellationToken cancellationToken)
    {
        // p0262: cancel the run BEFORE releasing the lease (moved here from the deleted
        // StaleJobDetector). A stale heartbeat means the owning replica is gone, so this
        // is mostly a formality for THIS replica, but the cross-process
        // RunCancelRequestedEvent marks the run cancelled for any live consumer and the
        // projection. p0459: the release names the lease's OWN run, never just the ticket.
        if (candidate.RunId is { Length: > 0 } runId)
        {
            cancellationRegistry.TryCancel(runId, "stale-lease-reaped");
            await eventPublisher.PublishAsync(
                new RunCancelRequestedEvent(runId, "stale-lease-reaped", timeProvider.GetUtcNow()),
                cancellationToken);
        }
        await lease.ReleaseAsync(candidate.Project, candidate.TicketId, candidate.RunId, cancellationToken);
        logger.LogWarning(
            "Reaped crashed lease {Project}/{Ticket} (run={Run}, job={Job}) — DB heartbeat stale, "
            + "owning replica gone: run cancelled + lease released; the ticket is reclaimable",
            candidate.Project, candidate.TicketId.Value, candidate.RunId ?? "—", candidate.JobId ?? "—");
    }
}
