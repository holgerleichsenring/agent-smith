using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-10b0: a GitHub repository's open pull requests, most recently updated first, and the
/// comments on them — two repository-wide feeds (issue comments, review comments) read since a
/// time and sorted by creation, so three calls a repository a cycle. The feeds filter on UPDATED
/// time; a comment is kept only when it was created after the time.
/// </summary>
public sealed class GitHubOpenPullRequests(IGitHubClient client, string owner, string repo, string repoUrl) : IOpenPullRequestLister
{
    private const int PageSize = 100;

    public bool ReadsCommentsPerPullRequest => false;

    public async Task<OpenPullRequestsPage> ListOpenAsync(string? resume, int maxPages, CancellationToken ct)
    {
        var start = int.TryParse(resume, out var page) ? page : 1;
        var prs = await client.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest { State = ItemStateFilter.Open, SortProperty = PullRequestSort.Updated, SortDirection = SortDirection.Descending },
            new ApiOptions { PageSize = PageSize, PageCount = maxPages, StartPage = start });
        var items = prs.Select(p => new OpenPullRequest(p.Number.ToString(), p.HtmlUrl, p.Head?.Sha, p.Base?.Sha, p.Head?.Ref,
            p.User?.Login, [.. (p.Labels ?? []).Select(l => l.Name)], p.CreatedAt, p.UpdatedAt)).ToList();
        var cut = prs.Count >= PageSize * maxPages;
        return new OpenPullRequestsPage(items, cut, cut ? (start + maxPages).ToString() : null);
    }

    public async Task<IReadOnlyList<PrSweepComment>> CommentsSinceAsync(
        IReadOnlyList<OpenPullRequest> pullRequests, DateTimeOffset since, CancellationToken ct)
    {
        var open = pullRequests.Select(p => p.Number).ToHashSet(StringComparer.Ordinal);
        var issue = await client.Issue.Comment.GetAllForRepository(owner, repo,
            new IssueCommentRequest { Since = since, Sort = IssueCommentSort.Created, Direction = SortDirection.Ascending });
        var review = await client.PullRequest.ReviewComment.GetAllForRepository(owner, repo,
            new PullRequestReviewCommentRequest { Since = since, Sort = PullRequestReviewCommentSort.Created, Direction = SortDirection.Ascending });
        return [.. issue.Select(c => Comment(Number(c.HtmlUrl), c.Id, c.CreatedAt, c.Body, c.User?.Login, c.AuthorAssociation.StringValue))
            .Concat(review.Select(c => Comment(Number(c.PullRequestUrl), c.Id, c.CreatedAt, c.Body, c.User?.Login, c.AuthorAssociation.StringValue)))
            .OfType<PrSweepComment>().Where(c => open.Contains(c.PrNumber) && c.CreatedAt > since).OrderBy(c => c.CreatedAt)];
    }

    private PrSweepComment? Comment(string? number, long id, DateTimeOffset at, string? body, string? login, string? association) =>
        number is null || login is null ? null
            : new PrSweepComment(number, id.ToString("D20"), at, body ?? string.Empty,
                new PrCommentAuthor(repoUrl, $"{owner}/{repo}", login, login) { Association = association });

    // .../pull/4#issuecomment-1, .../issues/4#..., or the API's .../pulls/4.
    private static string? Number(string? url) =>
        url is null ? null : System.Text.RegularExpressions.Regex.Match(url, @"/(?:pull|pulls|issues)/(\d+)") is { Success: true } m ? m.Groups[1].Value : null;
}
