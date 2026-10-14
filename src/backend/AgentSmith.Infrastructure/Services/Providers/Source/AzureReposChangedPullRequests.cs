using AgentSmith.Contracts.Sweep;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-9e6e: an Azure Repos repository's active pull requests with a reviewer at "Wait for
/// author" (-5), the creator included — we never vote -5. The list carries no vote time, so each is
/// named by (pull request, voters) and the sweep nudges it once; a cut read resumes at the offset it
/// stopped. Every pull request with a reviewer is also listed RefreshOnly, named by every reviewer's
/// vote, the creator's and zeros included — a changed or reset vote is a new name, listed again.
/// </summary>
public sealed class AzureReposChangedPullRequests(
    Func<CancellationToken, Task<GitHttpClient>> connect, string project, string repoName, string repoUrl)
{
    private const int PageSize = 100;

    public async Task<ChangedPage> ChangedSinceAsync(string? resume, int maxPages, CancellationToken ct)
    {
        var client = await connect(ct);
        var skip = int.TryParse(resume, out var offset) ? offset : 0;
        var items = new List<ChangedItem>();
        for (var page = 0; page < maxPages; page++, skip += PageSize)
        {
            var prs = await client.GetPullRequestsAsync(project, repoName,
                new GitPullRequestSearchCriteria { Status = PullRequestStatus.Active }, skip: skip, top: PageSize, cancellationToken: ct);
            items.AddRange(prs.SelectMany(Items));
            if (prs.Count < PageSize) return new ChangedPage(items);
        }
        return new ChangedPage(items, Cut: true, Resume: skip.ToString());
    }

    private IEnumerable<ChangedItem> Items(GitPullRequest pr)
    {
        var reviewers = (pr.Reviewers ?? []).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        if (reviewers.Count == 0) yield break;
        var voters = reviewers.Where(r => r.Vote == -5).Select(r => r.Id).OrderBy(i => i).ToList();
        if (voters.Count > 0) yield return Item(pr, $"{pr.PullRequestId}|{string.Join(',', voters)}", refreshOnly: false);
        yield return Item(pr, $"refresh|{pr.PullRequestId}|{string.Join(',', reviewers.Select(r => $"{r.Id}:{r.Vote}"))}", refreshOnly: true);
    }

    private ChangedItem Item(GitPullRequest pr, string dedupe, bool refreshOnly) => new(DateTimeOffset.UtcNow,
        PrUrl: $"{repoUrl}/pullrequest/{pr.PullRequestId}", HeadRef: pr.SourceRefName, SameRepository: pr.ForkSource is null,
        Dedupe: dedupe, RefreshOnly: refreshOnly);
}
