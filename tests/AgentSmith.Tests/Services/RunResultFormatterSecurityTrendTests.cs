using System.Text;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class RunResultFormatterSecurityTrendTests
{
    [Fact]
    public void AppendSecurityTrend_Null_WritesNothing()
    {
        var sb = new StringBuilder();
        RunResultFormatter.AppendSecurityTrend(sb, null);
        sb.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendSecurityTrend_WithTrend_WritesTable()
    {
        var current = CreateSnapshot(critical: 2, high: 3, medium: 5, retained: 10);
        var previous = CreateSnapshot(critical: 3, high: 5, medium: 4, retained: 12);
        var trend = new SecurityTrend(
            NewFindings: 0,
            ResolvedFindings: 2,
            CriticalDelta: -1,
            HighDelta: -2,
            TotalScans: 5,
            AverageCost: 0.03m,
            Previous: previous,
            Current: current);

        var sb = new StringBuilder();
        RunResultFormatter.AppendSecurityTrend(sb, trend);
        var result = sb.ToString();

        result.Should().Contain("## Security Trend");
        result.Should().Contain("| Metric | Last Scan | This Scan | Delta |");
        result.Should().Contain("| Critical | 3 | 2 | -1 |");
        result.Should().Contain("| High | 5 | 3 | -2 |");
        result.Should().Contain("| Medium | 4 | 5 | +1 |");
        result.Should().Contain("| Total | 12 | 10 | -2 |");
        result.Should().Contain("**New findings:** 0");
        result.Should().Contain("**Resolved:** 2");
        result.Should().Contain("**Scans:** 5");
    }

    [Fact]
    public void AppendSecurityTrend_NoPrevious_ShowsDash()
    {
        var current = CreateSnapshot(critical: 2, high: 3, medium: 5, retained: 10);
        var trend = new SecurityTrend(
            NewFindings: 10,
            ResolvedFindings: 0,
            CriticalDelta: 2,
            HighDelta: 3,
            TotalScans: 1,
            AverageCost: 0.02m,
            Previous: null,
            Current: current);

        var sb = new StringBuilder();
        RunResultFormatter.AppendSecurityTrend(sb, trend);
        var result = sb.ToString();

        result.Should().Contain("| Critical | - | 2 | +2 |");
        result.Should().Contain("| High | - | 3 | +3 |");
    }

    [Fact]
    public void AppendSecurityTrend_ZeroDelta_ShowsZero()
    {
        var current = CreateSnapshot(critical: 3, high: 5, medium: 4, retained: 12);
        var previous = CreateSnapshot(critical: 3, high: 5, medium: 4, retained: 12);
        var trend = new SecurityTrend(
            NewFindings: 0,
            ResolvedFindings: 0,
            CriticalDelta: 0,
            HighDelta: 0,
            TotalScans: 3,
            AverageCost: 0.01m,
            Previous: previous,
            Current: current);

        var sb = new StringBuilder();
        RunResultFormatter.AppendSecurityTrend(sb, trend);
        var result = sb.ToString();

        result.Should().Contain("| Critical | 3 | 3 | 0 |");
        result.Should().Contain("| High | 5 | 5 | 0 |");
    }

    private static SecurityRunSnapshot CreateSnapshot(
        int critical = 0, int high = 0, int medium = 0, int retained = 0)
    {
        return new SecurityRunSnapshot(
            Date: DateTimeOffset.UtcNow,
            Branch: "main",
            FindingsCritical: critical,
            FindingsHigh: high,
            FindingsMedium: medium,
            FindingsRetained: retained,
            FindingsAutoFixed: 0,
            ScanTypes: ["StaticPatternScan"],
            NewSinceLast: 0,
            ResolvedSinceLast: 0,
            TopCategories: [],
            CostUsd: 0.01m);
    }
}
