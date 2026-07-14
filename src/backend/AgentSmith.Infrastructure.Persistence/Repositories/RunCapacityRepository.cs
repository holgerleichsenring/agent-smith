using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0336: data access for the per-run capacity ledger over a SCOPED unit of work.
/// The reservation flip is a read-sum-write inside a transaction so concurrent
/// admissions can never both squeeze past the budget (the spawn probe is the
/// backstop for the residual TOCTOU). A null budget means unbounded (fail-open).
/// </summary>
public sealed class RunCapacityRepository(IUnitOfWork unitOfWork)
{
    public async Task UpsertFootprintAsync(
        string runId, string footprintJson, long cpuNanos, long memBytes, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<RunCapacity>().FirstOrDefaultAsync(x => x.RunId == runId, ct);
        if (existing is null)
            unitOfWork.Add(new RunCapacity
            {
                RunId = runId, FootprintJson = footprintJson,
                TotalCpuNanos = cpuNanos, TotalMemBytes = memBytes, Reserved = false,
            });
        else
        {
            existing.FootprintJson = footprintJson;
            existing.TotalCpuNanos = cpuNanos;
            existing.TotalMemBytes = memBytes;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<bool> TryReserveAsync(
        string runId, long? budgetCpuNanos, long? budgetMemBytes, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var row = await unitOfWork.Set<RunCapacity>().FirstOrDefaultAsync(x => x.RunId == runId, ct);
        // No recorded footprint (a legacy or resumed pre-p0336 run) → fail-open: we
        // can't size it, so we don't gate it. An already-reserved row is idempotent.
        if (row is null || row.Reserved) { await tx.CommitAsync(ct); return true; }
        if (!await FitsAsync(row, budgetCpuNanos, budgetMemBytes, ct)) { await tx.CommitAsync(ct); return false; }
        row.Reserved = true;
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task ReleaseAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<RunCapacity>().Where(x => x.RunId == runId).ExecuteDeleteAsync(ct);

    public Task<RunCapacity?> GetAsync(string runId, CancellationToken ct) =>
        unitOfWork.Set<RunCapacity>().AsNoTracking().FirstOrDefaultAsync(x => x.RunId == runId, ct);

    public async Task<IReadOnlyList<RunCapacity>> GetManyAsync(
        IReadOnlyCollection<string> runIds, CancellationToken ct) =>
        await unitOfWork.Set<RunCapacity>().AsNoTracking()
            .Where(x => runIds.Contains(x.RunId)).ToListAsync(ct);

    private async Task<bool> FitsAsync(RunCapacity row, long? budgetCpu, long? budgetMem, CancellationToken ct)
    {
        var reserved = unitOfWork.Set<RunCapacity>().Where(x => x.Reserved);
        var usedCpu = await reserved.SumAsync(x => x.TotalCpuNanos, ct);
        var usedMem = await reserved.SumAsync(x => x.TotalMemBytes, ct);
        return (budgetCpu is null || usedCpu + row.TotalCpuNanos <= budgetCpu)
            && (budgetMem is null || usedMem + row.TotalMemBytes <= budgetMem);
    }
}
