using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0466: owns what a run's TERMINAL transition means — the set-once status, the cost
/// that has to be true when the run is revisited, the capacity the run stops holding,
/// and the queue entry a rejected launch leaves behind. Split out of
/// <see cref="RunEventApplier"/> like every other projection: the applier routes an
/// event, a projection owns what it means.
/// <para>
/// p0336: the release is here because this is the single choke point every terminal
/// status flows through (success/failed/cancelled/enforced). Optional budget so the
/// many DB-free test sites keep composing it.
/// </para>
/// </summary>
public sealed class RunFinalizationProjection(
    QueuedRunProjection queuedRuns,
    ICapacityBudget? capacityBudget = null)
{
    public async Task ApplyAsync(IUnitOfWork uow, RunFinishedEvent e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is null) return;
        // p0330: terminal transitions are SET-ONCE. 'queued' keeps FinishedAt null
        // (it is a WAITING state, see below), so a non-null FinishedAt means a
        // terminal status already landed — a late RunFinished from a force-killed
        // pod must not overwrite 'cancelled', and vice versa.
        if (run.FinishedAt is not null) return;
        await FinishAsync(uow, run, e, ct);
        // p0336: a terminal run stops holding compute — free its budget reservation.
        // A waiting state (queued / waiting_for_input) keeps FinishedAt null and its
        // reservation, so the run is guaranteed its footprint when it (re)launches.
        if (run.FinishedAt is not null && capacityBudget is not null)
            await capacityBudget.ReleaseAsync(e.RunId, ct);
    }

    private async Task FinishAsync(
        IUnitOfWork uow, Run run, RunFinishedEvent e, CancellationToken ct)
    {
        run.Status = e.Status;
        // p0320c: "queued" is a WAITING state, not a terminal one — the row stays
        // in the active set (FinishedAt null) until it launches or is cancelled.
        // p0327: "waiting_for_input" is the same shape — parked on a question,
        // no lease, no sandbox, resumed onto this very row.
        run.FinishedAt = e.Status is "queued" or "waiting_for_input" ? null : e.Timestamp;
        run.Summary = e.Summary;
        // p0355: cost must be TRUE on revisit. The run-end total (RunFinishedEvent.
        // CostUsd) is authoritative when present, but older/leaking producers emit
        // null — and the DB projector never accumulated per-call cost onto the row,
        // so those runs persisted $0 despite real RunLlmCall rows. Fall back to the
        // sum of the persisted per-call costs so the detail read returns the real
        // total, not a stale zero.
        run.CostTotalUsd = e.CostUsd ?? await SumLlmCostAsync(uow, e.RunId, ct);
        // p0320c TOCTOU backstop: the orchestrator cannot reach this DB, so its
        // capacity rejection surfaces as RunFinished status="queued" — project a
        // queue entry from the run row so the next attempt reuses THIS row.
        if (e.Status == "queued")
            await queuedRuns.UpsertEntryAsync(uow, run, e.Timestamp, ct);
        await uow.SaveChangesAsync(ct);
    }

    // p0355: sum the run's persisted per-call costs — the fallback total when the
    // run-end event carried no cost. Zero when no calls were recorded.
    private static async Task<decimal> SumLlmCostAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var costs = await uow.Set<RunLlmCall>().AsNoTracking()
            .Where(c => c.RunId == runId).Select(c => c.CostUsd).ToListAsync(ct);
        return costs.Sum();
    }
}
