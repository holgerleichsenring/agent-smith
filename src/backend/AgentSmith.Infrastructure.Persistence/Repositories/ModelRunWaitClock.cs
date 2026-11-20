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
    public async Task<IReadOnlyList<Run>> OverdueAsync(
        IReadOnlyList<Run> running, TimeSpan maxWallTime, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(running);
        var ids = running.Select(r => r.Id).ToList();
        var waits = await unitOfWork.Set<ModelRunWait>().AsNoTracking().Where(w => ids.Contains(w.RunId)).ToListAsync(ct);
        var waited = waits.ToDictionary(w => w.RunId, w => w.Waited(now), StringComparer.Ordinal);
        return [.. running.Where(r => now - r.StartedAt - waited.GetValueOrDefault(r.Id) > maxWallTime)];
    }
}
