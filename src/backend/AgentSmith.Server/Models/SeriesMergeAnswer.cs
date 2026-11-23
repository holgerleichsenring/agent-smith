namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dce: the 200 answer of a merge from the series page — the merge run's id is on the ticket's page;
/// this carries what it did per repo.</summary>
public sealed record SeriesMergeAnswer(string TicketId, IReadOnlyList<SeriesMergeOutcomeView> PullRequests);
