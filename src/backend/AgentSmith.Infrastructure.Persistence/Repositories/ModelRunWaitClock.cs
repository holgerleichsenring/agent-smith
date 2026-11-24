using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-20-24bd: a run's wall time without the time it waited for its model place — the wait belongs to another
/// run's turn on the deployment, not to this run. Run.StartedAt stays what the operator sees.
/// </summary>
public sealed class ModelRunWaitClock(IUnitOfWork unitOfWork)
{
    /// <summary>The runs of <paramref name="running"/> past <paramref name="maxWallTime"/> once their waits are taken out.</summary>
    public Task<IReadOnlyList<Run>> OverdueAsync(
        IReadOnlyList<Run> running, TimeSpan maxWallTime, DateTimeOffset now, CancellationToken ct) =>
        OverdueAsync(running, _ => maxWallTime, now, ct);

    /// <summary>2026-11-23-c81ec: the same with a ceiling per run — a PoC run's is pocs.run_wall_time_seconds. A ceiling
    /// of zero or less holds no run.</summary>
    public async Task<IReadOnlyList<Run>> OverdueAsync(
        IReadOnlyList<Run> running, Func<Run, TimeSpan> ceilingOf, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ceilingOf);
        ArgumentNullException.ThrowIfNull(running);
        var ids = running.Select(r => r.Id).ToList();
        var waits = await unitOfWork.Set<ModelRunWait>().AsNoTracking().Where(w => ids.Contains(w.RunId)).ToListAsync(ct);
        var waited = waits.ToDictionary(w => w.RunId, w => w.Waited(now), StringComparer.Ordinal);
        return [.. running.Where(r => ceilingOf(r) is var ceiling && ceiling > TimeSpan.Zero
            && now - r.StartedAt - waited.GetValueOrDefault(r.Id) > ceiling)];
    }

    /// <summary>2026-11-22-d434b: whether <paramref name="runId"/> waits for its model place now — its wait row is open.</summary>
    public Task<bool> IsWaitingAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<ModelRunWait>().AsNoTracking().AnyAsync(w => w.RunId == runId && w.WaitingSince != null, ct);

    /// <summary>2026-11-23-31dci: since when each run waiting for its model place now has waited, by run id. A wait row
    /// lives only while its run does, so the read is bounded by the running runs.</summary>
    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> WaitingSinceAsync(CancellationToken ct)
    {
        var open = await unitOfWork.Set<ModelRunWait>().AsNoTracking().Where(w => w.WaitingSince != null)
            .Select(w => new { w.RunId, w.WaitingSince }).ToListAsync(ct);
        return open.ToDictionary(w => w.RunId, w => w.WaitingSince!.Value, StringComparer.Ordinal);
    }
}
