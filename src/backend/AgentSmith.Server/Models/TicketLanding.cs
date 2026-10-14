using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Server.Models;

/// <summary>
/// How a ticket's pull requests ended, read from their records: merged when every record ended and at
/// least one merged, unmerged when every record ended and none merged, null otherwise — no record, or any
/// record open, unsupported or unread.
/// </summary>
public static class TicketLanding
{
    public const string Merged = "merged";
    public const string Unmerged = "unmerged";

    public static string? Of(IReadOnlyList<TicketPullRequestView> pullRequests) =>
        OfStates([.. (pullRequests ?? throw new ArgumentNullException(nameof(pullRequests))).Select(p => p.State)]);

    /// <summary>2026-10-14-dbceg: the rule over the records' states alone, shared with the landing recorder.</summary>
    public static string? OfStates(IReadOnlyList<string> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0 || !states.All(PrReviewStates.IsEnded)) return null;
        return states.Any(s => s == PrReviewStates.Merged) ? Merged : Unmerged;
    }
}
