namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9c: the requests for changes that still STAND on a pull request — GitHub's latest
/// review per reviewer in state CHANGES_REQUESTED, Azure DevOps' newest -5 vote of a reviewer still at
/// -5 — each with the host's time for it. The pull request's own author is never among them; bots are
/// flagged, trust is the caller's.
/// </summary>
public interface IPrReviewActReader
{
    Task<IReadOnlyList<PrReviewNote>> ChangesRequestedAsync(string prUrl, CancellationToken cancellationToken);
}
