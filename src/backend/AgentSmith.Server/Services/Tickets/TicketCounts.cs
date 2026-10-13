using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the list's counts over EVERY ticket read, independent of the bucket filter
/// and the page. A working ticket whose newest attempt is not running is queued — a reservation,
/// or a parked run whose relaunch holds a place or a fresh lease.
/// <para>2026-10-13-9a59b: and the review side — waiting on review, done, open pull requests, the
/// tickets done this calendar month (UTC) with the attempts they took, and the last one done.</para>
/// <para>2026-10-13-9a59h: and the stuck ones.</para>
/// </summary>
public sealed class TicketCounts(TimeProvider clock)
{
    private const string Running = "running";

    public TicketCountsView Of(IReadOnlyList<TicketSummaryView> tickets, decimal monthCost, bool truncated)
    {
        var working = tickets.Where(t => t.Bucket == TicketBucket.Working).ToList();
        var running = working.Count(t => t.Status == Running);
        var done = tickets.Where(t => t.Bucket == TicketBucket.Done && t.Since is not null).ToList();
        return new TicketCountsView(
            tickets.Count(t => t.Bucket == TicketBucket.NeedsYou),
            working.Count,
            running,
            working.Count - running,
            tickets.Count(t => t.Bucket == TicketBucket.Idle),
            tickets.Where(t => t.Bucket == TicketBucket.NeedsYou).Min(t => t.Since),
            monthCost,
            truncated,
            tickets.Count(t => t.Bucket == TicketBucket.WaitingOnReview),
            done.Count,
            tickets.Sum(t => t.PullRequests.Count(p => p.State == PrReviewStates.Open)),
            DoneThisMonth(done),
            done.MaxBy(t => t.Since) is { } last ? new TicketLastDone(last.Project, last.TicketId, last.Since!.Value) : null,
            tickets.Count(t => t.Bucket == TicketBucket.Stuck));
    }

    private TicketDoneCount DoneThisMonth(IReadOnlyList<TicketSummaryView> done)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var month = done.Where(t => t.Since!.Value.UtcDateTime is var at && at.Year == now.Year && at.Month == now.Month).ToList();
        return new TicketDoneCount(month.Count, month.Sum(t => t.AttemptCount));
    }
}
