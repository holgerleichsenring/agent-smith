using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-17-b34ea: the money the cap compares. 2026-09-01-b0d7 kept worker spend out of the cap's USD arm so a
/// subscription run would not stop on money it is not charged; the operator reversed that — a worker run over its
/// budget stops like any other. The figure is the larger of what this tracker saw (its priced calls plus what the
/// worker CLI reported) and the run's recorded-call ledger (2026-11-17-7c9bc), which the event publisher feeds for
/// every call, including the ones no call site handed this tracker. The larger, because each can miss what the other
/// holds: the ledger only sees calls under the run's frame, the tracker only what its call sites track.
/// </summary>
public sealed partial class PipelineCostTracker
{
    private RecordedCallLedger? _recordedCalls;

    /// <summary>Counts the run's recorded calls toward the cap from now on.</summary>
    public void CountRecordedCalls(RecordedCallLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        lock (_gate) _recordedCalls = ledger;
    }

    /// <summary>The spend the cap's USD arm compares — recorded calls, worker-reported cost included.</summary>
    public decimal BudgetCostUsd
    {
        get { lock (_gate) return BudgetCostUsdLocked(); }
    }

    private decimal BudgetCostUsdLocked() =>
        Math.Max(_accruedUsd + _workerCalls.ReportedCostUsd, _recordedCalls?.CostUsd ?? 0m);
}
