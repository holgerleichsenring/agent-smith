using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-20-a52f: the order runs wait in for a place on one key — the open waits recorded with that key, oldest
/// WaitingSince first (the row id breaks a tie). Only a LIVE waiter stands in the queue: its run is unfinished and beat
/// within <see cref="WaiterFreshFor"/>, or it has no Run row yet (the start event may lag) and began waiting within
/// <see cref="ModelRunSlotSweep.RowlessGrace"/>. A dead waiter therefore holds a freed place back at most until that bound,
/// when the liveness reaper and the sweep end it anyway. The few rows are judged in memory: SQLite cannot compare a
/// DateTimeOffset in SQL.
/// </summary>
public sealed class ModelRunWaitQueue(TimeProvider clock)
{
    /// <summary>How fresh a waiting run's beat must be to keep its turn — the liveness bound
    /// (ActiveRunReaper.LeaseFreshFor), past which the run is ended as interrupted.</summary>
    public static readonly TimeSpan WaiterFreshFor = TimeSpan.FromMinutes(3); // not a limit: the liveness reaper's own bound

    /// <summary>How many live runs wait on <paramref name="key"/> ahead of <paramref name="runId"/> — every live waiter
    /// when the run has recorded no wait yet.</summary>
    public async Task<int> AheadOfAsync(IUnitOfWork unitOfWork, string key, string runId, CancellationToken ct)
    {
        var waits = await unitOfWork.Set<ModelRunWait>().AsNoTracking()
            .Where(w => w.Key == key && w.WaitingSince != null && w.RunId != runId).ToListAsync(ct);
        if (waits.Count == 0) return 0;
        var own = await unitOfWork.Set<ModelRunWait>().AsNoTracking()
            .FirstOrDefaultAsync(w => w.RunId == runId && w.Key == key && w.WaitingSince != null, ct);
        var ids = waits.Select(w => w.RunId).ToList();
        var runs = await unitOfWork.Set<Run>().AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.FinishedAt, r.HeartbeatAt, r.StartedAt })
            .ToDictionaryAsync(r => r.Id, r => (r.FinishedAt, Beat: Latest(r.HeartbeatAt, r.StartedAt)), StringComparer.Ordinal, ct);
        var now = clock.GetUtcNow();
        bool Live(ModelRunWait wait) => runs.TryGetValue(wait.RunId, out var run)
            ? run.FinishedAt is null && now - run.Beat <= WaiterFreshFor
            : now - wait.WaitingSince!.Value <= ModelRunSlotSweep.RowlessGrace;
        return waits.Count(w => Live(w) && (own is null || Before(w, own)));
    }

    // The liveness reaper's sign of life: a resumed run's StartedAt is newer than its last beat before the park.
    private static DateTimeOffset Latest(DateTimeOffset? beat, DateTimeOffset started) =>
        beat is { } at && at > started ? at : started;

    private static bool Before(ModelRunWait wait, ModelRunWait own) =>
        wait.WaitingSince < own.WaitingSince || (wait.WaitingSince == own.WaitingSince && wait.Id < own.Id);
}
