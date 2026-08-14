using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0421: the ONE gate that decides whether a run delivered.
/// <para>
/// It reads the accounts every phase gave of itself against the branch (p0420) and
/// nothing else. Its predecessor asked whether THIS run had committed code and grew six
/// signals teaching that question its exceptions — expectsCodeChanges, shipsCode,
/// perRepoCommittedChange, expectedChangeRepos, the ledger, the changed paths — plus a
/// preset list that exempted whole pipelines from being checked at all. Every one of
/// them was an exception to a question that should not have been asked.
/// </para>
/// <para>
/// Operator's rule, and the reason this replaces rather than joins: two verdicts on the
/// same question means a wrong verdict has two possible authors.
/// </para>
/// </summary>
public static class RunDeliveryGate
{
    /// <summary>
    /// A run that ratified NO criteria is not judged here — inventing a requirement
    /// nobody stated is how the old gate came to fail runs that had delivered.
    /// <para>
    /// But criteria that were ratified and never accounted for are a GAP, not a pass:
    /// silence there means no phase ever measured itself, which is the hollow success
    /// the gate exists to prevent.
    /// </para>
    /// </summary>
    public static DeliveryVerdict Evaluate(RunAccounts accounts, int ratifiedCriteria)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var all = accounts.All;
        if (all.Count == 0)
            return ratifiedCriteria == 0
                ? DeliveryVerdict.Ok()
                : DeliveryVerdict.Fail(
                    $"The run ratified {ratifiedCriteria} criterion(s) and accounted for none of "
                    + "them — no phase measured itself against the branch, so there is nothing "
                    + "to show it delivered. Recorded as FAILED.");

        var unaccounted = all.Where(a => a.Problem is not null).ToList();
        if (unaccounted.Count > 0)
            return DeliveryVerdict.Fail(
                "The run could not be accounted for: "
                + string.Join("; ", unaccounted.Select(a => $"{a.RepoKey} — {a.Problem}"))
                + ". An unaccounted run is not a delivered one.");

        var outstanding = Outstanding(accounts);
        if (outstanding.Count > 0)
            return DeliveryVerdict.Fail(
                $"{outstanding.Count} ratified criterion(s) are not satisfied by the branch:"
                + $"\n- {string.Join("\n- ", outstanding)}"
                + "\nRecorded as FAILED; what is missing is named above, not inferred.");

        return DeliveryVerdict.Ok();
    }

    private static IReadOnlyList<string> Outstanding(RunAccounts accounts) =>
    [
        .. accounts.Phases.SelectMany(phase =>
            phase.Accounts.SelectMany(account =>
                account.Outstanding.Select(criterion =>
                    $"{phase.PhaseId} / {account.RepoKey}: {criterion.Criterion}"
                    + (criterion.Note is null ? string.Empty : $" ({criterion.Note})"))))
    ];
}
