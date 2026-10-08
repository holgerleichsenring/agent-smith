namespace AgentSmith.Contracts.Providers;

/// <summary>2026-10-08-2123: one change of a ticket into a status, as the tracker's history records it.</summary>
public sealed record TicketStatusMove(TrackerActor Actor, DateTimeOffset At, string Status);
