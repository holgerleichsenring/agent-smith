namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: the ticket list's counts, over every ticket of the filtered project(s) —
/// never only the page. Working splits into running and queued; a parked run whose relaunch
/// holds a place or a fresh lease counts as queued.
/// </summary>
/// <param name="MonthCost">Every run of the filtered project(s) started this month.</param>
/// <param name="Truncated">The read ceiling cut older tickets off.</param>
/// <param name="OpenPullRequests">2026-10-13-9a59b: open pull-request records over every ticket read.</param>
public sealed record TicketCountsView(
    int NeedsYou,
    int Working,
    int Running,
    int Queued,
    int Idle,
    DateTimeOffset? OldestNeedsYouSince,
    decimal MonthCost,
    bool Truncated,
    int WaitingOnReview,
    int Done,
    int OpenPullRequests,
    TicketDoneCount DoneThisMonth,
    TicketLastDone? LastDone);
