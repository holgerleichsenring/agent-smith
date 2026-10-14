namespace AgentSmith.Contracts.Runs;

/// <summary>2026-10-13-9a59d: the channel an attempt's cause came through.</summary>
public static class AttemptChannels
{
    public const string TicketComment = "ticket_comment";
    public const string PullRequest = "pull_request";
    public const string StatusMove = "status_move";

    /// <summary>Asked for by name in chat; the envelope names no requester.</summary>
    public const string Requested = "requested";

    /// <summary>A previous attempt exists and no act was read.</summary>
    public const string Retrigger = "retrigger";

    /// <summary>The ticket's first attempt.</summary>
    public const string Pickup = "pickup";

    /// <summary>2026-10-13-9a59m: an approval in the ticket's own conversation.</summary>
    public const string Operator = "operator";

    /// <summary>2026-10-13-9a59n: Retry after an infrastructure end — the same work, unchanged.</summary>
    public const string Retry = "retry";

    /// <summary>2026-10-13-8762p: a default overruled on the ticket page.</summary>
    public const string Overrule = "overrule";

    /// <summary>2026-10-13-8762v: agent-smith continued the ticket's spec series itself.</summary>
    public const string Series = "series";

    /// <summary>2026-10-13-8762v: the actor a series start names.</summary>
    public const string SeriesActor = "agent-smith";
}
