namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: starts a series' runs on the queued path — a queued run row, then the dispatch — seeded
/// with the series id, so any worker takes them and a lost push is pushed again from the row. Answers the run id.</summary>
public interface ISeriesRunLaunch
{
    Task<string> LaunchAsync(string project, string pipeline, string seriesRunId, CancellationToken cancellationToken);
}
