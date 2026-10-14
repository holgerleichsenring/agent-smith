namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59h: a person's act on the tracker after an attempt's cutoff, as the between-attempts check
/// stores it. An edit is not one: no tracker read says who edited or when.
/// </summary>
/// <param name="Kind"><see cref="Comment"/> or <see cref="StatusMove"/>.</param>
public sealed record TicketTrackerAct(string Kind, string? Who, DateTimeOffset At)
{
    public const string Comment = "comment";
    public const string StatusMove = "status-move";
}
