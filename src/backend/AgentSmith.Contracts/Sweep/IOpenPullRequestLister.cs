namespace AgentSmith.Contracts.Sweep;

/// <summary>
/// 2026-10-08-10b0: a repository's open pull requests and the comments on them since a time — what
/// the PR sweep needs to start pr-review, a label scan and comment commands without webhooks. A host
/// that reads comments pull request by pull request says so, and is handed a rotating few per cycle.
/// </summary>
public interface IOpenPullRequestLister
{
    bool ReadsCommentsPerPullRequest { get; }

    Task<OpenPullRequestsPage> ListOpenAsync(string? resume, int maxPages, CancellationToken cancellationToken);

    Task<IReadOnlyList<PrSweepComment>> CommentsSinceAsync(
        IReadOnlyList<OpenPullRequest> pullRequests, DateTimeOffset since, CancellationToken cancellationToken);
}
