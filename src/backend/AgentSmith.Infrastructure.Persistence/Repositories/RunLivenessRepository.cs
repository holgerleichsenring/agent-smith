using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-5f89e: the run row's own liveness — the beat the driving process renews, and
/// the running rows nobody renews any more. Kept out of <see cref="RunRepository"/>, which
/// sits at its file-length ratchet row.
/// </summary>
public sealed class RunLivenessRepository(IUnitOfWork unitOfWork)
{
    /// <summary>A single-column update; a finished row keeps its last beat.</summary>
    public Task RenewHeartbeatAsync(string runId, DateTimeOffset at, CancellationToken ct) =>
        unitOfWork.Set<Run>()
            .Where(r => r.Id == runId && r.FinishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HeartbeatAt, at), ct);

    /// <summary>
    /// Running, unfinished, unflagged rows whose last sign of life is older than
    /// <paramref name="staleAfter"/>. The last sign is the later of HeartbeatAt and StartedAt:
    /// StartedAt is the first beat of a run younger than its first renewal, and a run
    /// resumed from a park has StartedAt reset while its HeartbeatAt still dates from before
    /// the park. Only status running qualifies — a queued or parked run has no driver to
    /// miss. SQLite cannot compare DateTimeOffset in SQL, so the time filter is client-side,
    /// as the wall-time and cancel scans do it.
    /// </summary>
    public async Task<IReadOnlyList<Run>> GetStaleRunningRunsAsync(
        TimeSpan staleAfter, DateTimeOffset now, CancellationToken ct)
    {
        var running = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null && !r.CancelRequested && r.Status == "running")
            .ToListAsync(ct);
        return running.Where(r => now - LastSignOfLife(r) > staleAfter).ToList();
    }

    private static DateTimeOffset LastSignOfLife(Run run) =>
        run.HeartbeatAt is { } beat && beat > run.StartedAt ? beat : run.StartedAt;
}
