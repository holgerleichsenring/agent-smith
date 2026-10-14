namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59h: one thing that happened on a ticket after its last attempt failed.
/// </summary>
/// <param name="Kind">comment | status-move (the tracker), edit (the check saw another fingerprint;
/// timed at the check, no author), pr-review (a person's pull-request entry).</param>
/// <param name="Who">The author as the source recorded it; null for an edit.</param>
/// <param name="Url">The pull request of a pr-review entry.</param>
public sealed record TicketActedSinceView(string Kind, string? Who, DateTimeOffset At, string? Url = null);
