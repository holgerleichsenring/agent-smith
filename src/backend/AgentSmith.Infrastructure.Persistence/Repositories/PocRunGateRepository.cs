using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933db: the one serialisation of a PoC's runs, across replicas. In ONE transaction of the scoped unit of work
/// it moves Poc.LastRunId to the minted run id — only when the stored value is unchanged since it was read and names no
/// live run — and inserts the queued run row: no claim without a row, no row without a claim. A missing or finished run
/// is free; so is a queued row with no stored request that nothing claimed within StaleAfter, because nothing else ever
/// ends such a row.
/// </summary>
public sealed class PocRunGateRepository(IUnitOfWork unitOfWork, InitRunRepository runs, TimeProvider clock)
{
    private const string QueuedStatus = "queued";

    public async Task<PocClaim> ClaimAsync(PocClaimRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var held = await HeldAsync(request.PocId, ct);
        if (held is not null && held != request.HandoverRunId && await IsLiveAsync(held, request.StaleAfter, ct))
            return PocClaim.HeldBy(held);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var moved = await unitOfWork.Set<Poc>().Where(p => p.Id == request.PocId && p.LastRunId == held)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.LastRunId, request.RunId), ct);
        if (moved == 0)
        {
            await transaction.RollbackAsync(ct);
            return PocClaim.HeldBy(await HeldAsync(request.PocId, ct));
        }
        await runs.CreateQueuedRunAsync(request.RunId, request.Project, request.Pipeline, request.Repos, request.Summary, ct);
        if (request.RevisionId is { } revision)
            await unitOfWork.Set<PocRevision>().Where(r => r.Id == revision)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.RunId, request.RunId), ct);
        await transaction.CommitAsync(ct);
        return PocClaim.Taken;
    }

    /// <summary>Whether <paramref name="runId"/> names a run that is still under way.</summary>
    public async Task<bool> IsLiveAsync(string runId, TimeSpan staleAfter, CancellationToken ct)
    {
        var run = await unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Id == runId)
            .Select(r => new { r.FinishedAt, r.Status, r.QueuedRequestJson, r.ClaimedAt, r.StartedAt }).FirstOrDefaultAsync(ct);
        if (run is null || run.FinishedAt is not null) return false;
        var orphaned = run.Status == QueuedStatus && run.QueuedRequestJson is null && run.ClaimedAt is null
            && run.StartedAt < clock.GetUtcNow() - staleAfter;
        return !orphaned;
    }

    private Task<string?> HeldAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Id == pocId).Select(p => p.LastRunId).FirstOrDefaultAsync(ct);
}
