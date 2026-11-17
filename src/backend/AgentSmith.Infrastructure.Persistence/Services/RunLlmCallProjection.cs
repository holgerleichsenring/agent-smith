using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0355/p0369/p0404: owns what one finished LLM call means — its own row, the cost it
/// adds to the run LIVE, the cache and time facts it folds onto the run's metrics, and
/// the model time it charges to the step that spent it.
/// <para>
/// 2026-08-25-61f1: this is the table the cost rollups are summed from, so it is the table
/// a replay hurt most — one run reported forty-two times its true spend. The row now
/// carries the trail position of the call that produced it and is written only once, which
/// makes the sum a sum of calls again rather than of copies.
/// </para>
/// </summary>
public sealed class RunLlmCallProjection(
    RunStepTimeProjection stepTime,
    RunMetricsProjection metrics,
    ProjectedEventRecords records)
{
    public async Task ApplyAsync(
        IUnitOfWork uow, LlmCallFinishedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunLlmCall>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(CallFrom(e, eventSeq));
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        // 2026-11-17-166ab: the total is the sum of the rows, so a call projected after the
        // finish adds too (a replayed call was refused above by its trail position). The
        // metrics and step time of a terminal row stay as they landed.
        if (run is not null) run.CostTotalUsd += e.CostUsd;
        if (run is not null && run.FinishedAt is null)
        {
            metrics.Fold(run, e);
            await stepTime.FoldLlmAsync(uow, e, ct);
        }
        await uow.SaveChangesAsync(ct);
    }

    private static RunLlmCall CallFrom(LlmCallFinishedEvent e, long? eventSeq) =>
        new()
        {
            RunId = e.RunId, Role = e.Role, Phase = e.Phase, Model = e.Model,
            TokensIn = e.TokensIn, TokensOut = e.TokensOut, CostUsd = e.CostUsd, DurationMs = e.DurationMs,
            CachedTokensIn = e.CachedTokensIn, CacheCreationTokensIn = e.CacheCreationTokensIn,
            StepIndex = e.OriginStepIndex, // p0388a
            SpecId = e.OriginSpecId, MasterPass = e.OriginMasterPass, // 2026-10-13-9a59f
            EventSeq = eventSeq,
        };
}
