using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0337: deletes a run and everything it left behind. A terminal run is a
/// straight record delete; a non-terminal run is FORCE-CLEARED first — the
/// spawned pod terminated, the lease released, the queue entry removed (the
/// p0330 machinery) — so a delete never leaves a pod burning money or a held
/// lease blocking the ticket. If the pod kill fails, the record is KEPT
/// (PodTerminationFailed) for a retry.
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
    IServiceProvider services,
    RunRepository runs,
    RunDeletionRepository deletion,
    IActiveRunLease lease,
    ICapacityQueue queue,
    CancelledTicketFinalizer ticketFinalizer,
    ILogger<RunDeleter> logger)
{
    public async Task<RunDeleteOutcome> DeleteAsync(string runId, CancellationToken ct)
    {
        var run = await runs.GetRunDetailAsync(runId, ct);
        if (run is null) return RunDeleteOutcome.NotFound;
        if (run.FinishedAt is null && !await ForceClearAsync(run, ct))
            return RunDeleteOutcome.PodTerminationFailed;
        await deletion.DeleteAsync(runId, ct);
        logger.LogInformation("Deleted run {RunId} (status {Status})", runId, run.Status);
        return RunDeleteOutcome.Deleted;
    }

    // Bulk clear is terminal-only, so it never force-kills a live run.
    public Task<int> DeleteTerminalAsync(CancellationToken ct) => deletion.DeleteTerminalAsync(ct);

    // Reuses the p0330 cancel-enforcement order: kill the pod, release the lease,
    // drop the queue entry — before the rows are removed. Kill BEFORE finalize so
    // a terminate failure keeps the run intact for a retry.
    private async Task<bool> ForceClearAsync(Run run, CancellationToken ct)
    {
        logger.LogWarning("Force-clearing non-terminal run {RunId} (job {JobId}) before delete",
            run.Id, run.JobId ?? "—");
        if (!await TryTerminateJobAsync(run, ct)) return false;
        if (string.IsNullOrEmpty(run.Project) || string.IsNullOrEmpty(run.TicketId)) return true;
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
        return true;
    }

    private async Task<bool> TryTerminateJobAsync(Run run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.JobId)) return true; // in-process run: nothing spawned to kill
        var spawner = services.GetService<IJobSpawner>();
        if (spawner is null)
        {
            logger.LogWarning("Run {RunId} has job {JobId} but no IJobSpawner is registered", run.Id, run.JobId);
            return true;
        }
        try
        {
            await spawner.TerminateAsync(run.JobId!, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terminate failed for run {RunId} job {JobId} — keeping record", run.Id, run.JobId);
            return false;
        }
    }
}
