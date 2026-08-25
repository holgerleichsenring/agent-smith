using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-08-25-61f1: collapses the rows an earlier replay left behind, so the uniqueness this
/// phase installs can exist at all — a unique index over a table that already violates it
/// fails at migration time, on the operator's database, at deploy.
/// <para>
/// It runs BEFORE the migration and therefore on the schema the store is already on. Trail
/// rows go first because they are what the new index constrains; the per-call, per-step and
/// decision rows follow because the number that actually reached the operator was a cost,
/// and a cost is summed from the per-call rows an index over the trail never touches.
/// </para>
/// </summary>
public sealed class RunDuplicateRepair(
    ReplayedRunFinder finder,
    ReplayedRunRows rows,
    DuplicateRowSelector selector,
    RunCostRecomputer costs)
{
    public async Task<RunRepairReport> RepairAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var runs = await finder.FindAsync(uow, ct);
        if (runs.Count == 0) return RunRepairReport.Nothing;
        return new RunRepairReport(
            runs,
            await CollapseAsync<Entities.RunEvent>(uow, await rows.TrailAsync(uow, runs, ct), ct),
            await CollapseAsync<RunStep>(uow, await rows.StepsAsync(uow, runs, ct), ct),
            await CollapseAsync<RunLlmCall>(uow, await rows.LlmCallsAsync(uow, runs, ct), ct),
            await CollapseAsync<RunDecision>(uow, await rows.DecisionsAsync(uow, runs, ct), ct),
            await costs.RecomputeAsync(uow, runs, ct));
    }

    private async Task<int> CollapseAsync<T>(
        IUnitOfWork uow, IReadOnlyList<RepairRow> candidates, CancellationToken ct) where T : class
    {
        var superfluous = selector.Superfluous(candidates);
        if (superfluous.Count == 0) return 0;
        return await uow.Set<T>()
            .Where(row => superfluous.Contains(EF.Property<long>(row, "Id")))
            .ExecuteDeleteAsync(ct);
    }
}
