using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-9749: the rail that keeps NOT APPLICABLE from becoming a silent pass.
/// <para>
/// An account that satisfied nothing is not a delivery, whatever it declined to judge, or
/// the third disposition is simply a way to pass a phase having proven nothing. The rule
/// has a trap of its own: refusing while the outstanding list is empty produces a failure
/// with no criterion named and no repair possible, which is the shape the account was
/// built to end. So the refusal NAMES what went unjudged, and the antecedent each row
/// declared false — an operator reading it can overrule one line rather than the verdict.
/// </para>
/// <para>
/// One type, read by the phase verdict and by the run gate alike. Two copies of this rule
/// is two authors for one wrong verdict, which is the objection that made the run gate a
/// single gate in the first place.
/// </para>
/// </summary>
public static class NothingProven
{
    /// <summary>
    /// The refusal, or null when the accounts prove something — or when nothing was
    /// declined, in which case the caller's outstanding rail owns the verdict and this one
    /// has nothing to add.
    /// </summary>
    public static string? Refusal(IReadOnlyList<SpecAccount> accounts, string subject)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (accounts.Any(a => a.ProvesSomething)) return null;

        var declined = Declined(accounts);
        if (declined.Count == 0) return null;

        return $"No criterion of the ratified {subject} is satisfied by the branch — "
            + $"{declined.Count} was/were declined as not applicable and none was proven:"
            + $"\n- {string.Join("\n- ", declined)}"
            + "\nNot applicable is not a pass. Either the branch proves one of these, or the "
            + "antecedent each declined criterion rests on is wrong and wants overruling.";
    }

    private static IReadOnlyList<string> Declined(IReadOnlyList<SpecAccount> accounts) =>
    [
        .. accounts.SelectMany(account => account.Declined.Select(criterion =>
            $"{account.RepoKey}: {criterion.Criterion}"
            + (criterion.Antecedent is null
                ? string.Empty
                : $" (declined — the base carries no {criterion.Antecedent})"))),
    ];
}
