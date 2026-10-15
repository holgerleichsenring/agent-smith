namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-14-dbcef: one row per edge — the dependent ticket must wait until the required ticket is
/// merged. Keyed like <see cref="ApprovedSeries"/>: the tracker CONNECTION name plus the ticket key.
/// The required ticket lives on the dependent's tracker connection — one <see cref="Tracker"/> column
/// serves both ends, so a requirement across two tracker connections is not supported.
/// <para>
/// Written once at filing and never updated or deleted, not even with the runs of either ticket. The
/// edge's filing instant is <see cref="EntityBase.CreatedAt"/>, which the save stamps on insert.
/// </para>
/// </summary>
public sealed class TicketRequirement : EntityBase
{
    public long Id { get; set; }

    /// <summary>The tracker CONNECTION's catalog name, for both tickets of the edge.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The dependent ticket's key, <c>&lt;provider&gt;-&lt;ticketId&gt;</c>.</summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>The dependent's id in the tracker's own spelling — a copy taken at filing.</summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The project the dependent was filed into — a copy taken at filing.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>The key of the ticket that must be merged first.</summary>
    public string RequiredTicketKey { get; set; } = string.Empty;

    /// <summary>The required ticket's id in the tracker's own spelling.</summary>
    public string RequiredTicketId { get; set; } = string.Empty;

    /// <summary>Whether the approver held runs.control at filing: the dependent may start once every requirement is met.</summary>
    public bool StartsWhenMet { get; set; }

    /// <summary>The conversation the filing happened in — a copy taken at filing.</summary>
    public string FiledInConversation { get; set; } = string.Empty;
}
