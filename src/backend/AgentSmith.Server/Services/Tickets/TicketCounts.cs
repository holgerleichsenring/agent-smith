using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: the list's counts over EVERY ticket read, independent of the bucket filter
/// and the page. A working ticket whose newest attempt is not running is queued — a reservation,
/// or a parked run whose relaunch holds a place or a fresh lease.
/// </summary>
public sealed class TicketCounts
{
    private const string Running = "running";

    public TicketCountsView Of(IReadOnlyList<TicketSummaryView> tickets, decimal monthCost, bool truncated)
    {
        var working = tickets.Where(t => t.Bucket == TicketBucket.Working).ToList();
        var running = working.Count(t => t.Status == Running);
        return new TicketCountsView(
            tickets.Count(t => t.Bucket == TicketBucket.NeedsYou),
            working.Count,
            running,
            working.Count - running,
            tickets.Count(t => t.Bucket == TicketBucket.Idle),
            tickets.Where(t => t.Bucket == TicketBucket.NeedsYou).Min(t => t.Since),
            monthCost,
            truncated);
    }
}
