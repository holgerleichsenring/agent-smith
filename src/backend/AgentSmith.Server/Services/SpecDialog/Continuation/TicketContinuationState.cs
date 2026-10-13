using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: everything the approval of a ticket's own conversation is decided over, read
/// once — the ticket as fetched now, whether a person closed it for work, the attempt whose series the
/// approval would continue, and that series.
/// </summary>
/// <param name="BasisRunId">The newest code attempt on any project sharing the tracker; null when no code
/// attempt worked the ticket.</param>
/// <param name="Series">The series' base id, or null when nothing named one yet.</param>
/// <param name="Basis">The latest published cut with specs, else the approval record's set.</param>
/// <param name="ExecutedIds">The specs the pointer names as executed.</param>
public sealed record TicketContinuationState(
    string TicketId,
    bool ClosedForWork,
    string? BasisRunId,
    string? Series,
    IReadOnlyList<BasisSpec> Basis,
    IReadOnlyList<string> ExecutedIds,
    ContinuationBasis Scope,
    SpecApprovalRecord? Record)
{
    public bool Worked => BasisRunId is not null;
}

/// <summary>One spec of the series an approval is decided against.</summary>
public sealed record BasisSpec(string SpecId, string Goal, string Yaml);

/// <summary>The state, or why it could not be read — an approval over an unread ticket is refused.</summary>
public sealed record TicketContinuationRead(TicketContinuationState? State, string? Failure)
{
    public static TicketContinuationRead Failed(string why) => new(null, why);
}
