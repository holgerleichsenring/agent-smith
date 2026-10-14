namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-14-dbceg: a ticket landed — its pull requests ended with one merged, or a person marked it
/// done. Keyed like <see cref="TicketRequirement"/>: the tracker CONNECTION name plus the ticket key.
/// Written once and never updated or deleted, not even with the runs of the ticket: the pull-request
/// records it was read from die with their newest run, the landing does not.
/// </summary>
public sealed class LandedTicket : EntityBase
{
    public long Id { get; set; }

    /// <summary>The tracker CONNECTION's catalog name.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The ticket's key, <c>&lt;provider&gt;-&lt;ticketId&gt;</c>.</summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>The ticket's id in the tracker's own spelling.</summary>
    public string TicketId { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;

    /// <summary>merged or by-hand.</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTimeOffset LandedAt { get; set; }

    /// <summary>The person who marked it by hand; null for a merge.</summary>
    public string? By { get; set; }

    public string? Note { get; set; }

    /// <summary>The newest merged pull request's URL; unindexed.</summary>
    public string? PrUrl { get; set; }
}
