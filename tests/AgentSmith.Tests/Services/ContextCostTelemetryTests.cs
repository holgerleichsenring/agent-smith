using System.Linq;
using AgentSmith.Application.Services.Metrics;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0356: tokens-per-work-item + cached share — the overlay's scaling signal.
public sealed class ContextCostTelemetryTests
{
    private static ProgressLedger Ledger(params ProgressStatus[] statuses) =>
        new(statuses.Select((s, i) => new ProgressLedgerEntry(i.ToString(), $"item {i}", s)).ToList());

    [Fact]
    public void Compute_TokensAndCacheOverDoneItems_ReportsPerItemAndCachedShare()
    {
        var ledger = Ledger(ProgressStatus.Done, ProgressStatus.Done, ProgressStatus.Pending);

        var report = ContextCostTelemetry.Compute(totalTokens: 100_000, cacheReadTokens: 75_000, ledger);

        report.TotalTokens.Should().Be(100_000);
        report.CachedShare.Should().BeApproximately(0.75, 0.001);
        report.DoneItems.Should().Be(2);
        report.TokensPerDoneItem.Should().Be(50_000);
    }

    [Fact]
    public void Compute_NoDoneItems_TokensPerItemNullNeverFakeZero()
    {
        var report = ContextCostTelemetry.Compute(10_000, 0, Ledger(ProgressStatus.Pending));

        report.TokensPerDoneItem.Should().BeNull();
        report.CachedShare.Should().Be(0);
    }

    [Fact]
    public void Compute_ZeroTokens_CachedShareZeroNotNaN()
    {
        var report = ContextCostTelemetry.Compute(0, 0, ProgressLedger.Empty);

        report.CachedShare.Should().Be(0);
        report.TokensPerDoneItem.Should().BeNull();
    }
}
