namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: the fix-bug ticket shape a bug outcome carries — the same fields
/// the existing fix-bug pipeline reads off a tracker ticket. p0315c files it
/// via the ITicketProvider create path.
/// </summary>
public sealed record BugTicketDraft(
    string Title,
    string Description,
    string? AcceptanceCriteria);
