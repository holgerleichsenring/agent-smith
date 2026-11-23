namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcj: a series ticket whose branch could not be read, so its changes are not listed.</summary>
public sealed record SeriesRunUnreadBranchView(string TicketId, string Why);
