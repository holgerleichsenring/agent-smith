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
        // The finish path stays authoritative: RunFinished overwrites the row with its own
        // total (or the per-call sum fallback), and a terminal row is never mutated by a
        // late replay.
        if (run is not null && run.FinishedAt is null)
        {
            run.CostTotalUsd += e.CostUsd;
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
            EventSeq = eventSeq,
        };
}
