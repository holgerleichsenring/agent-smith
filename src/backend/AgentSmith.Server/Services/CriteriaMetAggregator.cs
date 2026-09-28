using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// Sums the counted runs into the Criteria met snapshot: overall, per project, and per
/// project per month of the run's finish in UTC. A run whose snapshot yields no criterion
/// contributes nothing, not even to the run count.
/// </summary>
public sealed class CriteriaMetAggregator(RunCriteriaCounter counter)
{
    public CriteriaMetSnapshot Aggregate(IReadOnlyList<JudgedCodeRun> runs)
    {
        var counted = runs
            .Select(run => new CountedRun(
                run.Project, run.FinishedAt.UtcDateTime.ToString("yyyy-MM"), counter.Count(run)))
            .Where(run => run.Counts.Judged + run.Counts.NotApplicable > 0)
            .ToList();
        return new CriteriaMetSnapshot(
            counted.Count,
            Sum(counted),
            counted.GroupBy(run => run.Project)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(Project)
                .ToList());
    }

    private static CriteriaMetSnapshot.ProjectCriteria Project(IGrouping<string, CountedRun> project) =>
        new(project.Key, project.Count(), Sum(project), project
            .GroupBy(run => run.Month)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(month => new CriteriaMetSnapshot.MonthCriteria(month.Key, month.Count(), Sum(month)))
            .ToList());

    private static CriterionCounts Sum(IEnumerable<CountedRun> runs) =>
        runs.Aggregate(CriterionCounts.None, (sum, run) => sum.Plus(run.Counts));

    private sealed record CountedRun(string Project, string Month, CriterionCounts Counts);
}
