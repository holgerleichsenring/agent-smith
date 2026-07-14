namespace AgentSmith.Server.Models;

/// <summary>
/// p0329: the dashboard's expectation-metrics read shape. Two headline rates
/// per project, derived ONLY from production ratification outcomes (p0328):
/// ExpectationHitRate = verbatim / human-ratified (verbatim+edited+rejected)
/// — how often the drafted Soll block hit the mark unchanged; null until a
/// human has ratified anything. FirstPrAcceptance = (verbatim+edited) / all
/// negotiated runs — the share of runs whose first PR was built against a
/// human-accepted contract (unratified auto-stamps drag it down honestly).
/// Months carry the same counts bucketed by ratification month (UTC).
/// </summary>
public sealed record ExpectationMetricsSnapshot(
    int Total,
    IReadOnlyList<ExpectationMetricsSnapshot.ProjectMetrics> Projects)
{
    public sealed record ProjectMetrics(
        string Project,
        OutcomeCounts Counts,
        double? ExpectationHitRate,
        double FirstPrAcceptance,
        double? AverageEditDistance,
        IReadOnlyList<MonthMetrics> Months);

    public sealed record MonthMetrics(string Month, OutcomeCounts Counts);

    public sealed record OutcomeCounts(
        int Total, int Verbatim, int Edited, int Rejected, int Unratified);
}
