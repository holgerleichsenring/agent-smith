using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-15-114fb: whether a person's close mark still decides a ticket's bucket. The mark is never deleted
/// here; it is inert while not in force. It yields to the Done arm exactly where that arm answers, to an attempt
/// started after it, and to a person's pull-request entry later than the close plus
/// <see cref="PreviousAttempt.SkewMargin"/> — so a comment typed just before the close does not undo it.
/// </summary>
public static class TicketClosedRule
{
    /// <param name="doneAt">The Done arm's own time — a merge's newest end, else a stored landing; null when none.</param>
    public static bool InForce(
        TicketClosedMark mark, Run newest, IReadOnlyList<TicketPullRequestView> pullRequests, DateTimeOffset? doneAt)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(newest);
        ArgumentNullException.ThrowIfNull(pullRequests);
        if (doneAt is { } done && newest.StartedAt <= done) return false;
        if (newest.StartedAt > mark.ClosedAt) return false;
        var after = mark.ClosedAt + PreviousAttempt.SkewMargin;
        return !pullRequests.Any(p => p.PersonActivityAt > after);
    }
}
