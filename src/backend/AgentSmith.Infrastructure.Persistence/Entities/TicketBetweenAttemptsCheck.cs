namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59h: what the tracker said about a ticket after its last attempt failed — whether its
/// text still has the fingerprint that attempt fetched, and who acted on it since that attempt's cutoff.
/// One row per last attempt (RunId unique); a newer check of the same attempt overwrites it. Only what
/// the tracker said is kept here: the pull-request side is read from its own record at query time.
/// </summary>
public sealed class TicketBetweenAttemptsCheck : EntityBase
{
    public long Id { get; set; }

    /// <summary>The ticket's last attempt when the check ran.</summary>
    public string RunId { get; set; } = string.Empty;

    public DateTimeOffset CheckedAt { get; set; }

    /// <summary>The ticket's title, description and criteria still have the attempt's fingerprint.</summary>
    public bool FingerprintUnchanged { get; set; }

    /// <summary>The persons' tracker acts since the cutoff, as a JSON array of {kind comment | status-move, who, at}.</summary>
    public string ActsJson { get; set; } = "[]";

    /// <summary>2026-10-15-114fc: a person closed the ticket for work on the tracker (TicketClosure's answer).</summary>
    public bool ClosedForWork { get; set; }
}
