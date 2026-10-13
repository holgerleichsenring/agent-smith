using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59n: the stored Retry of a run. A request is UPSERTED: the worker may end one without an
/// attempt (the tracker refused the move), and a second Retry must then be recordable — it replaces who asked
/// and when. Two replicas meet at the unique RunId; the loser re-reads the winner's row and writes over it.
/// </summary>
public sealed class RunRetryRequestRepository(IUnitOfWork uow, IUniqueViolationTranslator violations)
{
    public Task<RunRetryRequest?> ForRunAsync(string runId, CancellationToken ct) =>
        uow.Set<RunRetryRequest>().AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId, ct);

    public async Task UpsertAsync(string runId, string requestedBy, DateTimeOffset at, CancellationToken ct)
    {
        var row = await FindAsync(runId, ct);
        if (row is null)
        {
            var entry = uow.Add(new RunRetryRequest { RunId = runId, RequestedBy = requestedBy, RequestedAt = at });
            try
            {
                await uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (violations.IsUniqueViolation(ex))
            {
                entry.State = EntityState.Detached;
                row = await FindAsync(runId, ct) ?? throw new InvalidOperationException(
                    $"The retry request of run {runId} was written by another replica and is gone.");
            }
        }
        row.RequestedBy = requestedBy;
        row.RequestedAt = at;
        await uow.SaveChangesAsync(ct);
    }

    private Task<RunRetryRequest?> FindAsync(string runId, CancellationToken ct) =>
        uow.Set<RunRetryRequest>().FirstOrDefaultAsync(r => r.RunId == runId, ct);
}
