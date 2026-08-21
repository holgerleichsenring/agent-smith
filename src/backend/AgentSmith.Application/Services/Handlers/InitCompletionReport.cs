using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0501: how a completion attempt is NAMED — once, for the per-repo record and for the
/// sentence the operator reads. There are three outcomes and they are said as three:
/// merged is finished; armed means the platform will merge it itself once the policy it
/// is waiting on passes, so nobody has to come back to it; refused means the pull
/// request is still open and wants a human. Collapsing armed into either neighbour
/// would report a lie in one direction or the other.
/// </summary>
public static class InitCompletionReport
{
    /// <summary>The per-repo status this outcome is recorded under (p0347's PullRequestsJson).</summary>
    public static string StatusOf(PullRequestCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => PullRequestStatuses.Completed,
            PullRequestCompletionOutcome.Armed => PullRequestStatuses.CompletionArmed,
            _ => PullRequestStatuses.CompletionRefused,
        };
    }

    /// <summary>What the step says when the launch did not carry the operator's auto-accept.</summary>
    public static string Untouched(int count) =>
        count == 0
            ? "Auto-accept was off; no pull request was opened."
            : $"Auto-accept was off; {count} pull request(s) stay open.";

    /// <summary>What the step says after it tried, keeping the three outcomes apart.</summary>
    public static string Describe(
        int attempted, IReadOnlyList<string> refusals, IReadOnlyList<string> armed)
    {
        ArgumentNullException.ThrowIfNull(refusals);
        ArgumentNullException.ThrowIfNull(armed);
        var merged = attempted - refusals.Count - armed.Count;
        var parts = new List<string> { $"Merged {merged}/{attempted} init pull request(s)." };
        if (armed.Count > 0)
            parts.Add($"Auto-complete armed, waiting on policy — {string.Join("; ", armed)}");
        if (refusals.Count > 0)
            parts.Add($"Still open — {string.Join("; ", refusals)}");
        return string.Join(" ", parts);
    }
}
