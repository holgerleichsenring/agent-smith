using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0337: deletes a run and everything it left behind. A terminal run is a
/// straight record delete; a non-terminal run is FORCE-CLEARED first — the lease
/// released, the queue entry removed (the p0330 machinery) — so a delete never leaves a
/// held lease blocking the ticket.
/// <para>
/// 2026-09-20-9f00: a run that FINISHED keeps the deliberate hands-off — its ticket
/// is the operator's to move. A run with no result does not: force-clearing releases
/// the lease, the native status only moves at run-end so the ticket is still in the
/// trigger statuses, and re-pickup is gated on the lease — so the next poll re-claims
/// the ticket as a fresh run at the BACK of the queue and the operator's cleanup undoes
/// itself. Cancel already answers this for the same states; delete now does too.
/// </para>
/// </summary>
public sealed class RunDeleter(
    RunRepository runs,
    RunDeletionRepository deletion,
    IActiveRunLease lease,
    ICapacityQueue queue,
    CancelledTicketFinalizer ticketFinalizer,
    ILogger<RunDeleter> logger,
    IReworkWatermark? watermark = null)
{
    public async Task<RunDeleteOutcome> DeleteAsync(string runId, CancellationToken ct)
    {
        var run = await runs.GetRunDetailAsync(runId, ct);
        if (run is null) return RunDeleteOutcome.NotFound;
        // 2026-10-08-0781: before the release can nudge the ticket — a deleted run's acts are withheld.
        if (watermark is not null) await watermark.WithholdRunAsync(runId, DateTimeOffset.UtcNow, ct);
        if (run.FinishedAt is null) await ForceClearAsync(run, ct);
        await deletion.DeleteAsync(runId, ct);
        logger.LogInformation("Deleted run {RunId} (status {Status})", runId, run.Status);
        return RunDeleteOutcome.Deleted;
    }

    // Bulk clear is terminal-only, so it never force-kills a live run.
    public Task<int> DeleteTerminalAsync(CancellationToken ct) => deletion.DeleteTerminalAsync(ct);

    // Reuses the p0330 cancel-enforcement order: release the lease, drop the queue
    // entry — before the rows are removed.
    private async Task ForceClearAsync(Run run, CancellationToken ct)
    {
        logger.LogWarning("Force-clearing non-terminal run {RunId} before delete", run.Id);
        if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) return;
        // p0459: release under THIS run's id. Force-clearing a run used to drop the
        // lease by ticket, which handed a NEWER run's ticket back to the poller and
        // put two runs on one branch.
        await lease.ReleaseAsync(run.Project, new TicketId(run.TicketId), run.Id, ct);
        await queue.RemoveAsync(run.Project, run.TicketId, ct);
        // 2026-09-20-9f00: disarm the ticket, or the next poll re-files it. CONDITIONAL by the
        // finalizer's own ownership guard — it skips when the ticket's lease names a newer run —
        // and fail-soft inside, so a tracker that will not answer cannot fail the delete. The
        // comment says what happened: a delete cancelled nothing.
        await ticketFinalizer.FinalizeAsync(run.Project, run.TicketId, run.Id,
            "<b>Agent Smith — Deleted</b><br/>The run was deleted by an operator before it finished.",
            ct);
    }
}
