namespace AgentSmith.Infrastructure.Migration.Services.SeriesOfOne;

/// <summary>2026-11-25-96aea: one standalone ticket in flight — its project, id, the newest run's title, and whether its start
/// waits on its master (2026-11-26-a12ea: a queued RequirementsMet start, never requirement rows alone).</summary>
public sealed record SeriesOfOneCandidate(string Project, string TicketId, string? Title, bool AwaitsStart);
