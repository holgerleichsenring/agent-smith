namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-15-114fa: a person closed a ticket — "I won't continue", which is not a landing, so dependents keep
/// waiting. Keyed like <see cref="LandedTicket"/>: the tracker CONNECTION name plus the ticket key. Unlike a
/// landing it is a decision a person may take again: a second close overwrites it and a Reopen deletes it.
/// </summary>
public sealed class ClosedTicket : EntityBase
{
    public long Id { get; set; }

    /// <summary>The tracker CONNECTION's catalog name.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The ticket's key, <c>&lt;provider&gt;-&lt;ticketId&gt;</c>.</summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>The ticket's id in the tracker's own spelling.</summary>
    public string TicketId { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    public DateTimeOffset ClosedAt { get; set; }

    /// <summary>The person who closed it.</summary>
    public string By { get; set; } = string.Empty;

    /// <summary>Why, trimmed; null when no note was given. Unbounded and unindexed.</summary>
    public string? Note { get; set; }

    /// <summary>A JSON array of the pull-request URLs this close closed on their hosts.</summary>
    public string ClosedPullRequestsJson { get; set; } = "[]";

    /// <summary>Whether this close closed the ticket on its tracker.</summary>
    public bool TrackerClosed { get; set; }
}
