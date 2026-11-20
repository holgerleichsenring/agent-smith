using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-20-24bd: frees model places and waits no executing run can own, conservatively, because Run.Status is
/// projected asynchronously: a place goes only when its run ENDED, or is queued or parked and has not beaten for the
/// liveness threshold, or has had no row for <see cref="RowlessGrace"/>. A running row is never swept — the liveness
/// reaper ends a dead one first. Freed runs are forgotten in this process (their door stays) and waiters are woken.
/// <para>2026-11-20-9936b: the same tick releases capacity ledger rows (<see cref="RunCapacity"/>) whose run ENDED, or that
/// have had no Run row for <see cref="RowlessReservationGrace"/> and no lease or queue entry that could be theirs — never a
/// queued, parked or running run's, which keep their reservation by design. It runs after the places are freed, so its
/// failure cannot undo them.</para>
/// </summary>
public sealed class ModelRunSlotSweep(IServiceScopeFactory scopeFactory, ModelRunSlotSignal signal, TimeProvider clock)
{
    /// <summary>A place or wait whose run has no row is swept only after this long: the run's start event may be late.</summary>
    public static readonly TimeSpan RowlessGrace = TimeSpan.FromMinutes(15); // not a limit: projection lag the sweep waits out

    public async Task<int> RunAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = clock.GetUtcNow();
        var places = await unitOfWork.Set<ModelRunSlot>().AsNoTracking().ToListAsync(cancellationToken);
        var waits = await unitOfWork.Set<ModelRunWait>().AsNoTracking().ToListAsync(cancellationToken);
        var ids = places.Select(p => p.RunId).Concat(waits.Select(w => w.RunId)).Distinct().ToList();
        // SQLite cannot compare a DateTimeOffset in SQL: the few rows are judged in memory.
        var runs = await unitOfWork.Set<Run>().AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        bool Orphaned(string runId, DateTimeOffset since) => runs.TryGetValue(runId, out var run)
            ? run.FinishedAt is not null
              || (run.Status is RunStatuses.Queued or RunStatuses.WaitingForInput && now - (run.HeartbeatAt ?? run.StartedAt) > staleAfter)
            : now - since > RowlessGrace;
        var freed = places.Where(p => Orphaned(p.RunId, p.AcquiredAt)).Select(p => p.RunId).Distinct().ToList();
        var forgotten = waits.Where(w => Orphaned(w.RunId, w.UpdatedAt)).Select(w => w.RunId).ToList();
        if (freed.Count > 0) await unitOfWork.Set<ModelRunSlot>().Where(p => freed.Contains(p.RunId)).ExecuteDeleteAsync(cancellationToken);
        if (forgotten.Count > 0) await unitOfWork.Set<ModelRunWait>().Where(w => forgotten.Contains(w.RunId)).ExecuteDeleteAsync(cancellationToken);
        foreach (var runId in freed) signal.Forgotten(runId);
        await ReleaseCapacityAsync(unitOfWork, now, cancellationToken);
        return freed.Count;
    }

    /// <summary>2026-11-20-9936b: a capacity reservation with no Run row is released only after this long. A reservation is
    /// taken before the run's row exists — the spawn funnel and init admission reserve, the claim takes the lease and
    /// enqueues the job, and the row appears only when the job dequeues (RunStarted) — so an admitted run can wait in the
    /// job queue for a long time without a row. The orphans this sweep is for are days to months old.</summary>
    public static readonly TimeSpan RowlessReservationGrace = TimeSpan.FromHours(24); // not a limit: refuses nothing, waits out a queued job

    /// <summary>A lease claimed this long before a reservation's last write may still be the claim that follows it.</summary>
    private static readonly TimeSpan ClaimSkew = TimeSpan.FromMinutes(1); // not a limit: clock skew between reserve and claim

    // 2026-11-20-9936b: a terminal run's row is released; a rowless one only when it is older than the long grace, no
    // lease names it or could still be its unattached claim, and no capacity-queue entry reserves it.
    private static async Task ReleaseCapacityAsync(IUnitOfWork unitOfWork, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await unitOfWork.Set<RunCapacity>().AsNoTracking()
            .Select(c => new { c.RunId, c.UpdatedAt }).ToListAsync(cancellationToken);
        if (rows.Count == 0) return;
        var ids = rows.Select(c => c.RunId).Distinct().ToList();
        var finished = await unitOfWork.Set<Run>().AsNoTracking().Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.FinishedAt is not null, cancellationToken);
        var leases = await unitOfWork.Set<ActiveRun>().AsNoTracking()
            .Select(a => new { a.RunId, a.ClaimedAt }).ToListAsync(cancellationToken);
        var queued = (await unitOfWork.Set<QueuedTicket>().AsNoTracking().Where(q => q.ReservedRunId != null)
            .Select(q => q.ReservedRunId!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        bool Held(string runId, DateTimeOffset since) => queued.Contains(runId)
            || leases.Any(a => a.RunId == runId || (a.RunId is null && a.ClaimedAt >= since - ClaimSkew));
        var released = rows.Where(c => finished.TryGetValue(c.RunId, out var ended)
                ? ended
                : now - c.UpdatedAt > RowlessReservationGrace && !Held(c.RunId, c.UpdatedAt))
            .Select(c => c.RunId).Distinct().ToList();
        if (released.Count > 0) await DeleteReleasedAsync(unitOfWork, released, cancellationToken);
    }

    /// <summary>The delete re-checks in SQL: a run whose row appeared or turned non-terminal since the read, or that a lease
    /// (an unattached one included) or a queue entry took up, keeps its reservation. Internal for its test.</summary>
    internal static Task<int> DeleteReleasedAsync(IUnitOfWork unitOfWork, IReadOnlyCollection<string> released, CancellationToken cancellationToken)
    {
        var runs = unitOfWork.Set<Run>();
        var leases = unitOfWork.Set<ActiveRun>();
        var entries = unitOfWork.Set<QueuedTicket>();
        return unitOfWork.Set<RunCapacity>()
            .Where(c => released.Contains(c.RunId) && !runs.Any(r => r.Id == c.RunId && r.FinishedAt == null)
                && !leases.Any(a => a.RunId == c.RunId || (a.RunId == null && !runs.Any(r => r.Id == c.RunId)))
                && !entries.Any(q => q.ReservedRunId == c.RunId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
