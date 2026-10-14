namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcei: a person's mark that a ticket landed — why it counts as landed.</summary>
public sealed record LandedByHandRequest(string? Note);
