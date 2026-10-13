namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: one ticket — its list row, its work branch, its attempts and its other runs.</summary>
/// <param name="Branch">The ticket's default work branch; a run handed a checkout branch worked elsewhere.</param>
/// <param name="Attempts">Oldest first, numbered from 1.</param>
public sealed record TicketDetailView(
    TicketSummaryView Summary,
    string Branch,
    IReadOnlyList<TicketAttemptView> Attempts,
    IReadOnlyList<TicketOtherRunView> OtherRuns);
