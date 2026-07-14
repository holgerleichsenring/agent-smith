using AgentSmith.Contracts.Expectations;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0329: turns raw ratification rows into the per-project metric snapshot.
/// Pure mapping (internal for the aggregation test). In-memory like the run
/// child hydration: one row per run keeps the set small, and SQLite could
/// not translate the month bucketing anyway.
/// </summary>
internal static class ExpectationMetricsAggregator
{
    public static ExpectationMetricsSnapshot Aggregate(
        IReadOnlyList<ExpectationOutcomeRow> rows) =>
        new(rows.Count, rows
            .GroupBy(r => r.Project)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(ProjectMetrics)
            .ToList());

    private static ExpectationMetricsSnapshot.ProjectMetrics ProjectMetrics(
        IGrouping<string, ExpectationOutcomeRow> project)
    {
        var counts = Count(project);
        var humanRatified = counts.Verbatim + counts.Edited + counts.Rejected;
        var edits = project.Where(r => r.Outcome == ExpectationOutcomes.Edited).ToList();
        return new ExpectationMetricsSnapshot.ProjectMetrics(
            project.Key,
            counts,
            humanRatified == 0 ? null : (double)counts.Verbatim / humanRatified,
            (double)(counts.Verbatim + counts.Edited) / counts.Total,
            edits.Count == 0 ? null : edits.Average(r => r.EditDistance),
            Months(project));
    }

    private static IReadOnlyList<ExpectationMetricsSnapshot.MonthMetrics> Months(
        IEnumerable<ExpectationOutcomeRow> rows) =>
        rows.GroupBy(r => r.RatifiedAt.UtcDateTime.ToString("yyyy-MM"))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ExpectationMetricsSnapshot.MonthMetrics(g.Key, Count(g)))
            .ToList();

    private static ExpectationMetricsSnapshot.OutcomeCounts Count(
        IEnumerable<ExpectationOutcomeRow> rows)
    {
        var list = rows.ToList();
        return new ExpectationMetricsSnapshot.OutcomeCounts(
            list.Count,
            list.Count(r => r.Outcome == ExpectationOutcomes.Verbatim),
            list.Count(r => r.Outcome == ExpectationOutcomes.Edited),
            list.Count(r => r.Outcome == ExpectationOutcomes.Rejected),
            list.Count(r => r.Outcome == ExpectationOutcomes.Unratified));
    }
}
