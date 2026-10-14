namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: one ticket — its list row, its work branch, its attempts and its other runs.</summary>
/// <param name="Branch">The ticket's default work branch; a run handed a checkout branch worked elsewhere.</param>
/// <param name="Attempts">Oldest first, numbered from 1.</param>
/// <param name="ActedSince">2026-10-13-9a59h: what happened after a failed last attempt — the check's tracker
/// acts, an edit the check saw, and persons' pull-request entries; empty when nothing was observed.</param>
public sealed record TicketDetailView(
    TicketSummaryView Summary,
    string Branch,
    IReadOnlyList<TicketAttemptView> Attempts,
    IReadOnlyList<TicketOtherRunView> OtherRuns,
    IReadOnlyList<TicketActedSinceView>? ActedSince = null);
