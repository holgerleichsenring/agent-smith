using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-08-25-61f1: brings a repaired run's stored cost back to what the run actually spent.
/// The total on the row is the per-call sum whenever the run-end event carried no cost of its
/// own, and it is also accumulated per call while the run is alive — so a run projected N
/// times reported N times its spend, and that number is what every cost rollup reads.
/// <para>
/// It only ever moves the total DOWN, to the sum of the calls that survived. A stored total
/// at or below that sum came from the run-end event, which is authoritative and is left
/// alone: deflating toward the surviving calls can correct an inflated run, never invent a
/// new number for a correct one.
/// </para>
/// </summary>
public sealed class RunCostRecomputer
{
    public async Task<IReadOnlyList<RunCostCorrection>> RecomputeAsync(
        IUnitOfWork uow, IReadOnlyList<string> runs, CancellationToken ct)
    {
        var corrections = new List<RunCostCorrection>();
        foreach (var runId in runs)
        {
            var correction = await CorrectAsync(uow, runId, ct);
            if (correction is not null) corrections.Add(correction);
        }
        return corrections;
    }

    private static async Task<RunCostCorrection?> CorrectAsync(
        IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var stored = await uow.Set<Run>().AsNoTracking()
            .Where(r => r.Id == runId).Select(r => (decimal?)r.CostTotalUsd).FirstOrDefaultAsync(ct);
        if (stored is not { } before) return null;
        var after = await SurvivingCostAsync(uow, runId, ct);
        if (before <= after) return null;
        await uow.Set<Run>().Where(r => r.Id == runId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.CostTotalUsd, after), ct);
        return new RunCostCorrection(runId, before, after);
    }

    private static async Task<decimal> SurvivingCostAsync(
        IUnitOfWork uow, string runId, CancellationToken ct) =>
        (await uow.Set<RunLlmCall>().AsNoTracking()
            .Where(c => c.RunId == runId).Select(c => c.CostUsd).ToListAsync(ct)).Sum();
}
