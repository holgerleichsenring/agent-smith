namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: lists a pull request's review — threads with their resolution and notes — by the
/// pull request's URL. Implemented by the source providers, reached by casting the repo's provider.
/// </summary>
public interface IPrReviewThreadReader
{
    Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken);
}
