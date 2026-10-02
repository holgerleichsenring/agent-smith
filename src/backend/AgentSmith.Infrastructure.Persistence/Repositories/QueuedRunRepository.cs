using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-02-5ab2b: a ticketless or resumed run's request on its own row — stored before the
/// Redis push, claimed by the one consumer that pops it, read back by the sweeper when the push
/// was lost. Every transition is one conditional update, because the copies of one request may
/// be popped on several replicas at once. Kept out of <see cref="RunRepository"/>, which sits at
/// its file-length ratchet row.
/// </summary>
public sealed class QueuedRunRepository(IUnitOfWork unitOfWork)
{
    private const string RunningStatus = "running";

    public Task StoreRequestAsync(string runId, string requestJson, DateTimeOffset at, CancellationToken ct) =>
        Rows(runId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.QueuedRequestJson, requestJson)
            .SetProperty(r => r.RequestEnqueuedAt, at)
            .SetProperty(r => r.ClaimedAt, (DateTimeOffset?)null), ct);

    /// <summary>
    /// Claims the row's stored request for this copy: ClaimedAt and HeartbeatAt = now where no
    /// copy claimed it yet. A lost update, or a row that already runs, is a duplicate; a row
    /// with no stored request (a ticket run) starts as it always did.
    /// </summary>
    public async Task<RunStartClaimOutcome> ClaimAsync(string runId, DateTimeOffset now, CancellationToken ct)
    {
        var claimed = await Rows(runId)
            .Where(r => r.QueuedRequestJson != null && r.ClaimedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedAt, now).SetProperty(r => r.HeartbeatAt, now), ct);
        if (claimed > 0) return RunStartClaimOutcome.Claimed;
        var row = await Rows(runId).AsNoTracking()
            .Select(r => new { r.Status, HasRequest = r.QueuedRequestJson != null })
            .FirstOrDefaultAsync(ct);
        return row is not null && (row.HasRequest || row.Status == RunningStatus)
            ? RunStartClaimOutcome.Duplicate
            : RunStartClaimOutcome.Unclaimed;
    }

    /// <summary>Unfinished, unflagged, unclaimed rows whose request was pushed longer than
    /// <paramref name="lostAfter"/> ago. Time filtered client-side: SQLite cannot compare a
    /// DateTimeOffset in SQL.</summary>
    public async Task<IReadOnlyList<Run>> GetUnclaimedAsync(TimeSpan lostAfter, DateTimeOffset now, CancellationToken ct) =>
        (await Pending().Where(r => r.ClaimedAt == null).ToListAsync(ct))
            .Where(r => now - (r.RequestEnqueuedAt ?? r.StartedAt) > lostAfter).ToList();

    /// <summary>Claimed rows whose later of HeartbeatAt and ClaimedAt is older than
    /// <paramref name="staleAfter"/> — the consumer that claimed them is gone.</summary>
    public async Task<IReadOnlyList<Run>> GetDeadClaimsAsync(TimeSpan staleAfter, DateTimeOffset now, CancellationToken ct) =>
        (await Pending().Where(r => r.ClaimedAt != null).ToListAsync(ct))
            .Where(r => now - LastSignOfLife(r) > staleAfter).ToList();

    /// <summary>Renews the push time of a request still unclaimed — after the sweeper pushed it again.</summary>
    public Task RenewEnqueuedAsync(string runId, DateTimeOffset at, CancellationToken ct) =>
        Rows(runId).Where(r => r.QueuedRequestJson != null && r.ClaimedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RequestEnqueuedAt, at), ct);

    private IQueryable<Run> Pending() =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.FinishedAt == null && !r.CancelRequested && r.QueuedRequestJson != null);

    private IQueryable<Run> Rows(string runId) => unitOfWork.Set<Run>().Where(r => r.Id == runId);

    private static DateTimeOffset LastSignOfLife(Run run) =>
        run.HeartbeatAt is { } beat && beat > run.ClaimedAt!.Value ? beat : run.ClaimedAt!.Value;
}
