namespace AgentSmith.Server.Models;

/// <summary>
/// The dashboard's Criteria met read shape: criteria of finished coding runs counted by
/// status, overall, per project, and per project per month of the run's finish (UTC).
/// <see cref="Runs"/> counts the runs that contributed at least one criterion.
/// </summary>
public sealed record CriteriaMetSnapshot(
    int Runs,
    CriterionCounts Counts,
    IReadOnlyList<CriteriaMetSnapshot.ProjectCriteria> Projects)
{
    public sealed record ProjectCriteria(
        string Project, int Runs, CriterionCounts Counts, IReadOnlyList<MonthCriteria> Months);

    public sealed record MonthCriteria(string Month, int Runs, CriterionCounts Counts);
}
