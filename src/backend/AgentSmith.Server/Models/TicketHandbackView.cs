namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: a posted hand-back as its run recorded it — the words and when.</summary>
public sealed record TicketHandbackView(string Text, DateTimeOffset At);
