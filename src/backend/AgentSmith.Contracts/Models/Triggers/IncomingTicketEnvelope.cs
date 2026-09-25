namespace AgentSmith.Contracts.Models.Triggers;

/// <summary>
/// Platform-neutral shape of an incoming ticket — built by each webhook handler from its
/// payload type, then handed to ProjectResolver. All fields except Labels are optional so
/// the same record covers GitHub issues (Labels + SourceRepoUrl), ADO work items (Labels +
/// AreaPath), Jira issues (Labels), and (in p0141) emails (ToAddress).
/// </summary>
public sealed record IncomingTicketEnvelope
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public string? AreaPath { get; init; }
    public string? SourceRepoUrl { get; init; }
    public string? ToAddress { get; init; }
    public string? TicketId { get; init; }
    public string? TicketUrl { get; init; }
    public string? Platform { get; init; }

    /// <summary>
    /// 2026-09-25-3c7aa: true when the approval store holds a record for this ticket — the FACT
    /// that somebody approved a specification for it, read where the envelope is built rather than
    /// asserted by a label on somebody else's board.
    /// <para>
    /// It is set on the two paths whose tickets are ROUTED — the poll and the webhook — and left
    /// false everywhere else, so an envelope built by a caller with no store behind it routes
    /// exactly as it did before this field existed. It is read BESIDE the two labels that also
    /// bind, never instead of them: a container or CLI run binds the in-memory store and would
    /// answer no to every ticket.
    /// </para>
    /// </summary>
    public bool HasApprovedRecord { get; init; }
}
