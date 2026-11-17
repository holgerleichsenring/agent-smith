using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-17-7c9bc: puts the run's call ledger on its context and, for a resumed segment, carries the
/// spend the earlier segments made — the same snapshot the cost tracker is seeded from, its priced and its
/// worker buckets added together, since the ledger keeps one channel for both.
/// </summary>
public static class RecordedCallAttach
{
    public static void To(PipelineContext pipeline, RecordedCallLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(ledger);
        pipeline.Set(ContextKeys.RecordedCalls, ledger);
        if (pipeline.TryGet<PriorSpendSnapshot>(ContextKeys.PriorSpend, out var prior) && prior is not null)
            ledger.Carry(CarryOf(prior));
    }

    public static RecordedCallCarry CarryOf(PriorSpendSnapshot prior) => new(
        prior.InputTokens + prior.WorkerInputTokens,
        prior.OutputTokens + prior.WorkerOutputTokens,
        prior.CacheReadTokens + prior.WorkerCacheReadTokens,
        prior.CacheCreateTokens + prior.WorkerCacheCreationTokens,
        prior.AccruedUsd + prior.WorkerReportedCostUsd);
}
