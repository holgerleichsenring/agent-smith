using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-10b0: an Azure Repos repository's active pull requests ($top/$skip; Azure DevOps lists
/// no updated time, so creation stands for it) and the comments of the few pull requests the sweep
/// hands it — its threads are read pull request by pull request, in rotation. System comments are
/// no comments; the author carries the repository and project ids the trust check takes.
/// </summary>
public sealed class AzureReposOpenPullRequests(
    Func<CancellationToken, Task<GitHttpClient>> connect, string project, string repoName, string repoUrl) : IOpenPullRequestLister
{
    private const int PageSize = 100;

    public bool ReadsCommentsPerPullRequest => true;

    public async Task<OpenPullRequestsPage> ListOpenAsync(string? resume, int maxPages, CancellationToken ct)
    {
        var client = await connect(ct);
        var skip = int.TryParse(resume, out var offset) ? offset : 0;
        var items = new List<OpenPullRequest>();
        for (var page = 0; page < maxPages; page++, skip += PageSize)
        {
            var prs = await client.GetPullRequestsAsync(project, repoName,
                new GitPullRequestSearchCriteria { Status = PullRequestStatus.Active }, skip: skip, top: PageSize, cancellationToken: ct);
            items.AddRange(prs.Select(Pr));
            if (prs.Count < PageSize) return new OpenPullRequestsPage(items);
        }
        return new OpenPullRequestsPage(items, true, skip.ToString());
    }

    public async Task<string?> HeadCommitMessageAsync(string sha, CancellationToken ct)
    {
        var client = await connect(ct);
        return (await client.GetCommitAsync(project, sha, repoName, cancellationToken: ct)).Comment;
    }

    public async Task<IReadOnlyList<PrSweepComment>> CommentsSinceAsync(
        IReadOnlyList<OpenPullRequest> pullRequests, DateTimeOffset since, CancellationToken ct)
    {
        var client = await connect(ct);
        var repository = await client.GetRepositoryAsync(project, repoName, cancellationToken: ct);
        var comments = new List<PrSweepComment>();
        foreach (var pr in pullRequests)
            foreach (var thread in (await client.GetThreadsAsync(project, repoName, int.Parse(pr.Number), cancellationToken: ct)).Where(t => t.IsDeleted != true))
                comments.AddRange((thread.Comments ?? []).Where(c => c.IsDeleted != true && c.CommentType != CommentType.System && c.Author?.Id is not null)
                    .Select(c => new PrSweepComment(pr.Number, $"{thread.Id:D10}.{c.Id:D5}", new DateTimeOffset(DateTime.SpecifyKind(c.PublishedDate, DateTimeKind.Utc)),
                        c.Content ?? string.Empty, new PrCommentAuthor(repoUrl, repository.Id.ToString(), c.Author.Id, c.Author.UniqueName ?? c.Author.Id)
                        { ProjectId = repository.ProjectReference.Id.ToString() }))
                    .Where(c => c.CreatedAt > since));
        return comments;
    }

    private OpenPullRequest Pr(GitPullRequest pr)
    {
        var created = new DateTimeOffset(DateTime.SpecifyKind(pr.CreationDate, DateTimeKind.Utc));
        return new OpenPullRequest(pr.PullRequestId.ToString(), $"{repoUrl}/pullrequest/{pr.PullRequestId}",
            pr.LastMergeSourceCommit?.CommitId, pr.LastMergeTargetCommit?.CommitId,
            pr.SourceRefName?.Replace("refs/heads/", string.Empty, StringComparison.Ordinal), pr.CreatedBy?.UniqueName,
            [.. (pr.Labels ?? []).Select(l => l.Name)], created, created);
    }
}
