using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// Counts one coding run's criteria by the status they ended on.
/// <para>
/// An operator's overrule applies only while the status it answered is still the snapshot's:
/// a re-judged run may have changed its mind since, and an overrule of a verdict nobody holds
/// any more would correct the wrong thing. Such an overrule is counted as stale instead.
/// </para>
/// </summary>
public sealed class RunCriteriaCounter
{
    public CriterionCounts Count(JudgedCodeRun run)
    {
        var acceptance = run.Acceptance;
        if (acceptance is null) return CriterionCounts.None;
        var overrules = run.Overrules.ToDictionary(o => o.CriterionKey, StringComparer.Ordinal);
        // 2026-10-12-6bfad: an account that could not be taken is not among the criteria —
        // AcceptanceView lists it apart, archived snapshots included.
        return acceptance.Criteria
            .Select(criterion => Counted(
                criterion.Status, overrules.GetValueOrDefault(CriterionKey.Of(criterion.Text))))
            .Aggregate(CriterionCounts.None, (sum, one) => sum.Plus(one));
    }

    private static CriterionCounts Counted(string status, CriterionOverrule? overrule)
    {
        if (overrule is null) return Of(status);
        if (overrule.MachineStatus != status) return Of(status) with { StaleOverrules = 1 };
        return Of(overrule.HumanStatus) with { Overruled = 1 };
    }

    private static CriterionCounts Of(string status) => status switch
    {
        AcceptanceCriterionStatuses.Met => CriterionCounts.None with { Met = 1 },
        AcceptanceCriterionStatuses.Unmet => CriterionCounts.None with { Unmet = 1 },
        AcceptanceCriterionStatuses.Unproven => CriterionCounts.None with { Unproven = 1 },
        AcceptanceCriterionStatuses.NotApplicable => CriterionCounts.None with { NotApplicable = 1 },
        _ => CriterionCounts.None,
    };
}
