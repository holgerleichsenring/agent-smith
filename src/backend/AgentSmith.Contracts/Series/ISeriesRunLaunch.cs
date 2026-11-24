namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: starts a series' runs on the queued path — a queued run row, then the dispatch — seeded
/// with the series id, so any worker takes them and a lost push is pushed again from the row. Answers the run id.</summary>
public interface ISeriesRunLaunch
{
    /// <param name="checkoutBranch">2026-11-24-f9c5b: the branch the run checks out (the series check of a series with a
    /// series branch); null checks out the default branch.</param>
    Task<string> LaunchAsync(string project, string pipeline, string seriesRunId, CancellationToken cancellationToken,
        string? checkoutBranch = null);

    /// <summary>2026-11-24-f9c5e: a refresh-base run that brings the series branch's final pull request up to date with the
    /// default branch it conflicts with; answers the run id.</summary>
    Task<string> RefreshAsync(string project, string repo, string branch, string prUrl, string prNumber, string baseBranch,
        CancellationToken cancellationToken);

    /// <summary>2026-11-17-0207d: whether a run the series launched has ended (or is gone).</summary>
    Task<bool> FinishedAsync(string runId, CancellationToken cancellationToken);
}
