namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: reads a pull request's CI on its head — check runs and commit statuses on GitHub, build-validation
/// policies and pull-request statuses on Azure DevOps, the head pipeline on GitLab. Implemented by the source
/// providers, reached by casting the repo's provider, as <see cref="IPrReviewStateReader"/> is.
/// </summary>
public interface IPullRequestCiReader
{
    Task<PullRequestCi> ReadCiAsync(string prUrl, CiReadRequest request, CancellationToken cancellationToken);
}
