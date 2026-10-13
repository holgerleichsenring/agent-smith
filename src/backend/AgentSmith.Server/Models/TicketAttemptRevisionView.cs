namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59d: the series revision an attempt worked from — its number and cause as written,
/// the series id, the executed spec ids it carried in, and the approval instant it worked from (null
/// when the set carried no approval).
/// </summary>
public sealed record TicketAttemptRevisionView(
    int Number, string? Cause, string? SeriesId, IReadOnlyList<string> CarriedIds, DateTimeOffset? ApprovalAt);
