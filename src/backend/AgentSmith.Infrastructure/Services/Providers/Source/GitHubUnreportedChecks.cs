using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92a: the checks GitHub waits for and nobody reports on a head — a commit status it EXPECTS, a required context
/// with no check run or status at all ('Waiting for status to be reported'). On a settled head with nothing else running nothing
/// is left that could report them, so they have not started and will not by themselves; otherwise they still wait. An absent
/// required check is required by definition.
/// </summary>
public static class GitHubUnreportedChecks
{
    /// <summary>Whether the unreported checks may be judged: the head settled, nothing reported still runs, and nothing failed —
    /// a red head is a fix's, whatever else never reported.</summary>
    public static bool Judged(IReadOnlyList<CiCheck> reported, IReadOnlyList<bool> expected, bool headSettled) =>
        headSettled && !reported.Where((_, i) => !expected[i]).Any(c => c.Outcome is CiCheckOutcome.Pending or CiCheckOutcome.Failed);

    /// <summary>The checks with each expected one decided and each absent required context added, not started.</summary>
    public static IReadOnlyList<CiCheck> Settle(IReadOnlyList<CiCheck> reported, IReadOnlyList<bool> expected, IReadOnlySet<string> required)
    {
        var checks = reported.Select((c, i) => expected[i] ? c with { Outcome = CiCheckOutcome.NotStarted } : c).ToList();
        var named = reported.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        checks.AddRange(required.Where(r => !named.Contains(r)).Order(StringComparer.Ordinal)
            .Select(r => new CiCheck(r, CiCheckOutcome.NotStarted, true, null)));
        return checks;
    }
}
