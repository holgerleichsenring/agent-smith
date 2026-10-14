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

    public static string? Of(IReadOnlyList<TicketPullRequestView> pullRequests)
    {
        ArgumentNullException.ThrowIfNull(pullRequests);
        if (pullRequests.Count == 0 || !pullRequests.All(p => PrReviewStates.IsEnded(p.State))) return null;
        return pullRequests.Any(p => p.State == PrReviewStates.Merged) ? Merged : Unmerged;
    }
}
