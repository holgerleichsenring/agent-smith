namespace AgentSmith.Contracts.Sweep;

/// <summary>
/// 2026-10-08-9e6e: a repository's open pull requests that may carry a rework act since a cursor —
/// a request for changes or a "Wait for author" vote — within a page budget, resuming a cut read.
/// </summary>
public interface IChangedPullRequestLister
{
    Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, string? resume, int maxPages, CancellationToken cancellationToken);
}
