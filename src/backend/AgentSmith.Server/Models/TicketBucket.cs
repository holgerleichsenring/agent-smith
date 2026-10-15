namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: who must act next on a ticket — the words the ticket list is filtered by.
/// Later members of the series insert buckets between these; the order the rule asks in is
/// needs_you, working, waiting_on_review, (stuck), done, then everything else.
/// </summary>
public static class TicketBucket
{
    /// <summary>The newest attempt is parked and no relaunch is under way: a person must answer.</summary>
    public const string NeedsYou = "needs_you";

    /// <summary>The newest attempt runs, waits in the capacity queue, or is being relaunched.</summary>
    public const string Working = "working";

    /// <summary>2026-10-13-9a59b: a pull request is open and no attempt runs, waits or needs a person.</summary>
    public const string WaitingOnReview = "waiting_on_review";

    /// <summary>
    /// 2026-10-13-9a59h: the last attempt failed and a check under a day old saw the ticket unchanged and
    /// untouched since — no tracker act, no person's pull-request entry. Never assumed: unchecked is idle.
    /// </summary>
    public const string Stuck = "stuck";

    /// <summary>Every pull request ended and at least one merged, and no attempt started since.</summary>
    public const string Done = "done";

    /// <summary>2026-10-15-114fb: a person closed the ticket and nothing since brought it back.</summary>
    public const string Closed = "closed";

    /// <summary>2026-10-15-114fb: every pull request closed and none merged.</summary>
    public const string PrClosed = "pr_closed";

    /// <summary>2026-10-15-114fb: the newest attempt failed and no pull request is open or landed.</summary>
    public const string Failed = "failed";

    /// <summary>Everything else (cancelled, or ended without a pull request); the row carries the newest status.</summary>
    public const string Idle = "idle";

    /// <summary>2026-10-14-dbcen: filed, no run on record, and a ticket it requires has not landed.</summary>
    public const string Blocked = "blocked";

    /// <summary>2026-10-14-dbcen: filed, no run on record, and nothing it requires is open.</summary>
    public const string Filed = "filed";

    /// <summary>Every bucket the list accepts as a filter.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        NeedsYou, Working, WaitingOnReview, Stuck, Done, Idle, Blocked, Filed, Closed, PrClosed, Failed,
    };
}
