namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: the fix-bug ticket shape a bug outcome carries — the same fields
/// the code pipeline reads off a tracker ticket. p0315c files it
/// via the ITicketProvider create path.
/// </summary>
public sealed record BugTicketDraft(
    string Title,
    string Description,
    string? AcceptanceCriteria)
{
    /// <summary>2026-11-15-67f1: the file text the design master wrote for this bug; null when no draft file wrote it.</summary>
    public string? Source { get; init; }
}
