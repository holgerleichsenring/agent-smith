namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-17-0207a: a series run — the durable owner of the tickets one Work it out conversation filed. It holds the
/// series' own state only; every step of work on it is a short run (a beat, a verify) with its own run id.
/// </summary>
/// <param name="Id">The series run's id.</param>
/// <param name="Conversation">The Work it out conversation (session id) the series came from.</param>
/// <param name="Principal">The approver; acts and follow-ups are theirs.</param>
/// <param name="MayStartRuns">The approver's runs.control, sampled at filing like every filed start.</param>
public sealed record SeriesRunRecord(
    string Id, string Conversation, string Project, string Principal, bool MayStartRuns, string State,
    IReadOnlyList<SeriesTicket> Tickets, DateTimeOffset StartedAt)
{
    public SeriesQuestion? Question { get; init; }

    public IReadOnlyList<SeriesVerdict> Verdicts { get; init; } = [];

    /// <summary>The criteria a follow-up already carried; a second failure of one is reported, never re-filed.</summary>
    public IReadOnlyList<string> FollowedUp { get; init; } = [];

    /// <summary>The master's thread, continued by every beat that judges.</summary>
    public string ThreadJson { get; init; } = "[]";

    /// <summary>The last conversation turn index the master has read.</summary>
    public int MessagesThrough { get; init; } = -1;

    /// <summary>The run id of the newest beat; the sweep starts none while it is unfinished.</summary>
    public string? BeatRunId { get; init; }

    /// <summary>The newest verify run's id.</summary>
    public string? VerifyRunId { get; init; }

    /// <summary>When the last beat ran: the stall check's clock, and null until the opening is posted.</summary>
    public DateTimeOffset? LastStatusAt { get; init; }

    /// <summary>The table as the last beat saw it, to tell a change; 2026-11-25-e94eb: never posted.</summary>
    public string LastTable { get; init; } = string.Empty;

    /// <summary>What the master's own loops have spent.</summary>
    public decimal MasterCostUsd { get; init; }

    /// <summary>True once the conversation was told the master's budget is spent.</summary>
    public bool BudgetSaid { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>2026-11-19-1b72a: the state a paused series returns to on resume; null unless paused.</summary>
    public string? PausedFrom { get; init; }

    /// <summary>2026-11-24-f9c5b: the branch its tickets integrate on in every repository they touch —
    /// agent-smith/series/&lt;id&gt;, cut from the default branch by the first ticket working there; null for a series opened
    /// before the branch existed (it runs as it did) and for one opened over a single ticket.</summary>
    public string? IntegrationBranch { get; init; }

    /// <summary>2026-11-24-f9c5e: per repo the final pull request the series opened from its branch; empty before.</summary>
    public IReadOnlyList<SeriesDelivery> Deliveries { get; init; } = [];

    /// <summary>2026-11-24-f9c5e: when a beat last delivered — read the final pull requests from their hosts.</summary>
    public DateTimeOffset? LastDeliveredAt { get; init; }

    /// <summary>The optimistic version the store compares on update.</summary>
    public int Version { get; init; }

    public bool IsActive => SeriesRunStates.IsActive(State);

    /// <summary>2026-11-23-31dcb: when the series last moved — the newest of its finish, its last beat and its start.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset LastActivityAt => new[] { FinishedAt, LastStatusAt, StartedAt }.Max() ?? StartedAt;
}
