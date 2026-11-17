using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-17-7c9bd: the run's cost summary summed from the calls it recorded — the source the run row
/// sums (2026-11-17-166ab) — instead of the cost tracker, which leaves worker calls out and sees only the
/// responses its call sites hand it. Rows are keyed by the execution stage each call's scope named ("Other"
/// when none); the total is every call's cost, worker calls included, plus what earlier segments carried;
/// the worker share is the worker calls alone. Unpriced tokens stay the tracker's pricing alarm.
/// </summary>
public static class RecordedCallSummary
{
    private const string NoStage = "Other";
    private const string EarlierSegments = "Prior segments";

    /// <summary>The summary of the run's ledger, or null when the context carries none or it holds no call.</summary>
    public static RunCostSummary? For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<RecordedCallLedger>(ContextKeys.RecordedCalls, out var ledger) || ledger is null) return null;
        var unpriced = PipelineCostTracker.GetOrCreate(pipeline).UnpricedTokensByModel;
        return Of(ledger.Calls, ledger.Carried, unpriced.Count > 0 ? unpriced : null);
    }

    public static RunCostSummary? Of(
        IReadOnlyList<RecordedCall> calls, RecordedCallCarry carried, IReadOnlyDictionary<string, long>? unpriced)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(carried);
        if (calls.Count == 0) return null;
        var byStage = ByStage(calls);
        if (!carried.IsEmpty)
            byStage[EarlierSegments] = new PhaseCost(EarlierSegments, (int)carried.TokensIn, (int)carried.TokensOut,
                (int)carried.CachedTokensIn, carried.CostUsd);
        return new RunCostSummary(byStage, calls.Sum(c => c.CostUsd) + carried.CostUsd, PerRepo(calls), unpriced,
            WorkerShare(calls));
    }

    private static Dictionary<string, PhaseCost> ByStage(IEnumerable<RecordedCall> calls) =>
        calls.GroupBy(c => string.IsNullOrWhiteSpace(c.Stage) ? NoStage : c.Stage!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Cost(g.ToList()), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, RepoCost>? PerRepo(IReadOnlyList<RecordedCall> calls)
    {
        var buckets = calls.Where(c => !string.IsNullOrEmpty(c.RepoName)).GroupBy(c => c.RepoName!).ToList();
        return buckets.Count == 0
            ? null
            : buckets.ToDictionary(b => b.Key, b => new RepoCost(ByStage(b), b.Sum(c => c.CostUsd)));
    }

    private static PhaseCost Cost(IReadOnlyList<RecordedCall> calls) => new(
        Models(calls), (int)calls.Sum(c => c.TokensIn), (int)calls.Sum(c => c.TokensOut),
        (int)calls.Sum(c => c.CachedTokensIn), calls.Sum(c => c.CostUsd));

    private static WorkerSpend? WorkerShare(IEnumerable<RecordedCall> calls)
    {
        var worker = calls.Where(c => c.IsWorker).ToList();
        return worker.Count == 0
            ? null
            : new WorkerSpend(Models(worker), worker.Count, worker.Sum(c => c.TokensIn), worker.Sum(c => c.TokensOut),
                worker.Sum(c => c.CachedTokensIn), worker.Sum(c => c.CacheCreationTokensIn), worker.Sum(c => c.CostUsd));
    }

    private static string Models(IEnumerable<RecordedCall> calls) => string.Join("+", calls
        .Select(c => c.Model).Where(m => !string.IsNullOrWhiteSpace(m))
        .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));
}
