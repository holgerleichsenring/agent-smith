namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-8762l: one code attempt's review rounds in event order; empty when it reviewed none.</summary>
public sealed record TicketReviewAttemptView(string RunId, int Number, IReadOnlyList<TicketReviewRoundView> Rounds);
