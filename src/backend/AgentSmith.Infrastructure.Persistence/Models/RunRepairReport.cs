using System.Globalization;

namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-25-61f1: what the repair removed and which runs' totals moved. A repair that
/// silently deletes rows is indistinguishable from data loss, and the numbers it corrects
/// were already published — so it says both, including when it found nothing.
/// </summary>
public sealed record RunRepairReport(
    IReadOnlyList<string> RepairedRuns,
    int TrailRowsRemoved,
    int StepRowsRemoved,
    int LlmCallRowsRemoved,
    int DecisionRowsRemoved,
    IReadOnlyList<RunCostCorrection> CostCorrections)
{
    public static RunRepairReport Nothing { get; } = new([], 0, 0, 0, 0, []);

    public int RowsRemoved => TrailRowsRemoved + StepRowsRemoved + LlmCallRowsRemoved + DecisionRowsRemoved;

    public string Describe() =>
        RepairedRuns.Count == 0
            ? "no run holds a recorded fact twice — nothing to repair."
            : string.Create(CultureInfo.InvariantCulture,
                $"{RepairedRuns.Count} run(s) held replayed records: removed {TrailRowsRemoved} trail, "
                + $"{StepRowsRemoved} step, {LlmCallRowsRemoved} per-call and {DecisionRowsRemoved} decision "
                + $"row(s); {DescribeCosts()}");

    private string DescribeCosts() =>
        CostCorrections.Count == 0
            ? "no run total had to move."
            : "corrected totals for " + string.Join(", ", CostCorrections.Select(c =>
                string.Create(CultureInfo.InvariantCulture, $"{c.RunId} ({c.Before} -> {c.After})"))) + ".";
}
