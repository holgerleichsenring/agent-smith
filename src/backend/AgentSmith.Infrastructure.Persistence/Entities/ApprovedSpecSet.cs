namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-09-17-0e79a: one row per spec-set key — the set a person APPROVED in the design
/// conversation, stored whole.
/// <para>
/// It is deliberately not the <see cref="TicketSpecSet"/> pointer and does not replace it. A
/// pointer points at git, and git is where a set lives ONCE a run has published it; what a
/// person ratified before any branch exists has no commit to point at, so it is content and
/// gets its own table.
/// </para>
/// </summary>
public sealed class ApprovedSpecSet : EntityBase
{
    public long Id { get; set; }

    /// <summary>The spec-set key: &lt;provider&gt;-&lt;ticketId&gt;. One ticket matching two
    /// projects of one tracker is the same work, so the PROJECT is not part of the identity.</summary>
    public string SpecKey { get; set; } = string.Empty;

    /// <summary>The tracker CONNECTION's catalog name. The spec key carries the tracker type and
    /// the ticket id, so two instances of one type collide on it; this is the other half of the
    /// identity.</summary>
    public string Tracker { get; set; } = string.Empty;

    /// <summary>The whole record — set, approval and approved repositories — as
    /// <c>SpecApprovalJson</c> writes it. This column is the content of record.</summary>
    public string RecordJson { get; set; } = string.Empty;

    /// <summary>
    /// The approval instant, its conversation and its principal, PROJECTED out of the record so
    /// an operator reading the table can see whose approval a row holds and when. They are
    /// written from the record and never read back into one: a row has one content column, and a
    /// second parse of the same fact is a second answer waiting to disagree.
    /// </summary>
    public DateTimeOffset ApprovedAt { get; set; }

    /// <inheritdoc cref="ApprovedAt"/>
    public string ApprovedInConversation { get; set; } = string.Empty;

    /// <inheritdoc cref="ApprovedAt"/>
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// 2026-09-25-c1f7: the TRACKER'S OWN ticket id — <c>DPG-1239</c>, not the spec key's
    /// <c>jira-dpg-1239</c>. A column rather than a derivation: <c>SpecSetKey.For</c> lowercases
    /// the id and replaces every non-alphanumeric character, so the tracker's spelling cannot be
    /// read back out of <see cref="SpecKey"/>, and a per-provider parser that guessed it is
    /// exactly what this repository refused to write for the ticket's label stamp. Empty on a row
    /// written before this phase — nothing can recover what was never stored, and such a row is
    /// simply never named in a discovery query.
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// 2026-09-25-c1f7: when a run FINISHED the ticket this record was written for. Null means
    /// "still outstanding", which is what bounds the discovery listing — without it the query
    /// grows with every approval this deployment ever made and re-fetches the tickets the parking
    /// statuses exist to exclude. It is not an approval instant and never competes with
    /// <see cref="ApprovedAt"/>: re-approving a ticket clears it, because there is new work.
    /// </summary>
    public DateTimeOffset? SatisfiedAt { get; set; }
}
