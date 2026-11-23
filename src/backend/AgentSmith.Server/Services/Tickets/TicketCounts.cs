using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the list's counts over EVERY ticket read, independent of the bucket filter
/// and the page. A working ticket whose newest attempt is not running is queued — a reservation,
/// or a parked run whose relaunch holds a place or a fresh lease.
/// <para>2026-10-13-9a59b: and the review side — done, open pull requests, the tickets done this calendar month (UTC)
/// with the attempts they took, and the last one done. 2026-11-23-d60d: no in-review count; a working ticket whose head
/// ended (its pull request waits) is neither running nor queued.</para>
/// <para>2026-10-13-9a59h: and the stuck ones. 2026-10-14-dbcen: and the blocked and filed never-run ones.
/// 2026-10-15-114fb: and the closed, failed and PR-closed ones; idle keeps the rest.</para>
/// <para>2026-11-23-31dcg: Your move counts holds, not a bucket of its own — a person's hold, and apart from it an admin's.</para>
/// </summary>
public sealed class TicketCounts(TimeProvider clock)
{
    private const string Running = "running";

    public TicketCountsView Of(IReadOnlyList<TicketSummaryView> tickets, decimal monthCost, bool truncated)
    {
        var working = tickets.Where(t => t.Bucket == TicketBucket.Working).ToList();
        var running = working.Count(t => t.Status == Running);
        var queued = working.Count(t => t.Status is RunStatuses.Queued or RunStatuses.WaitingForInput);
        var done = tickets.Where(t => t.Bucket == TicketBucket.Done && t.Since is not null).ToList();
        var yours = tickets.Where(t => t.Hold?.IsPersons == true).ToList();
        return new TicketCountsView(
            yours.Count,
            working.Count,
            running,
            queued,
            tickets.Count(t => t.Bucket == TicketBucket.Idle),
            yours.Min(t => t.Since),
            monthCost,
            truncated,
            done.Count,
            tickets.Sum(t => t.PullRequests.Count(p => p.State == PrReviewStates.Open)),
            DoneThisMonth(done),
            done.MaxBy(t => t.Since) is { } last ? new TicketLastDone(last.Project, last.TicketId, last.Since!.Value) : null,
            tickets.Count(t => t.Bucket == TicketBucket.Stuck),
            tickets.Count(t => t.Bucket == TicketBucket.Blocked),
            tickets.Count(t => t.Bucket == TicketBucket.Filed),
            tickets.Count(t => t.Bucket == TicketBucket.Closed),
            tickets.Count(t => t.Bucket == TicketBucket.Failed),
            tickets.Count(t => t.Bucket == TicketBucket.PrClosed),
            tickets.Count(t => t.Hold?.Addressee == AgentSmith.Application.Services.Holds.TicketHoldAddressees.Admin));
    }

    private TicketDoneCount DoneThisMonth(IReadOnlyList<TicketSummaryView> done)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var month = done.Where(t => t.Since!.Value.UtcDateTime is var at && at.Year == now.Year && at.Month == now.Month).ToList();
        return new TicketDoneCount(month.Count, month.Sum(t => t.AttemptCount));
    }
}
