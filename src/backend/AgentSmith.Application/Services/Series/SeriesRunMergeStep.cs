namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-23-31dcb: one ticket's open pull requests in the series' merge order — its depth, and whether it is
/// the one to merge now (<see cref="SeriesRunMergeWaits"/>); <paramref name="After"/> names the keys it waits for.</summary>
public sealed record SeriesRunMergeStep(string TicketId, string Key, int Depth, string Wait, IReadOnlyList<string> After);
