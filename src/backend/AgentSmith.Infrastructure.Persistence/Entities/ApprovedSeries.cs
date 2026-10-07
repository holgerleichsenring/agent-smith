namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-06-03c7f: one row per (tracker connection, ticket key) — the SERIES a person approved
/// in the design conversation, stored whole. It replaces the 2026-09-17-0e79a approved-spec-set row.
/// <para>
/// It is deliberately not the <see cref="TicketSeries"/> pointer. A pointer points at git, and git
/// is the RUN's truth once a branch carries the series; what a person ratified is also what every
/// reader without a clone works from — the dialog pane, the divergence check, the repositories a
/// run scopes by, the carrier, the cited sets and discovery — so it is content and has its own table.
/// </para>
/// <para>
/// <see cref="ContentJson"/> is the content of record. Every other column except the identity and
/// <see cref="SatisfiedAt"/> is PROJECTED out of it at save, so an operator reading the table sees
/// which series, which ticket, which repositories and whose approval a row holds; they are never
/// read back into a record, because a second parse of one fact is a second answer waiting to disagree.
/// </para>
/// </summary>
public sealed class ApprovedSeries : EntityBase
{
    public long Id { get; set; }

    /// <summary>The tracker CONNECTION's catalog name. The ticket key carries only the tracker
    /// type, so two instances of one type collide on it; this is the other half of the identity.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The ticket key, <c>&lt;provider&gt;-&lt;ticketId&gt;</c>. The project is not part of
    /// the identity: one ticket matching two projects of one tracker is the same work.</summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>The series' base id, <c>{yyyy-MM-dd}-{xxxx}</c>, minted at filing.</summary>
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>
    /// 2026-09-25-c1f7: the TRACKER'S OWN ticket id — <c>DPG-1239</c>, not the key's
    /// <c>jira-dpg-1239</c>. <c>TicketKey.For</c> lowercases and re-spells the id, so the tracker's
    /// spelling cannot be read back out of <see cref="TicketKey"/>; discovery names this column.
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The repositories the approval named, comma-separated by configured name.</summary>
    public string Repositories { get; set; } = string.Empty;

    /// <summary>The repository filing wrote the branch into; empty when the approval named none.</summary>
    public string CarryingRepo { get; set; } = string.Empty;

    /// <summary>The whole approval — the series with its goal, the approval, the repositories, the
    /// carrier and the cited website sets — as <c>SpecApprovalJson</c> writes it.</summary>
    public string ContentJson { get; set; } = string.Empty;

    /// <summary>The approval instant, its conversation and its principal, projected.</summary>
    public DateTimeOffset ApprovedAt { get; set; }

    /// <inheritdoc cref="ApprovedAt"/>
    public string ApprovedInConversation { get; set; } = string.Empty;

    /// <inheritdoc cref="ApprovedAt"/>
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// 2026-09-25-c1f7: when a run FINISHED the ticket. Null means "still outstanding", which bounds
    /// the discovery listing. Re-approving clears it, and so does a Retry (2026-10-06-03c7f): both
    /// are new work on the same ticket.
    /// </summary>
    public DateTimeOffset? SatisfiedAt { get; set; }
}
