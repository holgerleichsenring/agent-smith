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
        return freed.Count;
    }
}
