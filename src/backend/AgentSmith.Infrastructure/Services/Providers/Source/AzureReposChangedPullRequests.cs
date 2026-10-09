using AgentSmith.Contracts.Sweep;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-9e6e: an Azure Repos repository's active pull requests with a reviewer other than the
/// creator at "Wait for author" (-5). The list carries no vote time, so each is named by (pull
/// request, voters) and the sweep nudges it once; a cut read resumes at the offset it stopped.
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
            items.AddRange(prs.Select(Item).OfType<ChangedItem>());
            if (prs.Count < PageSize) return new ChangedPage(items);
        }
        return new ChangedPage(items, Cut: true, Resume: skip.ToString());
    }

    private ChangedItem? Item(GitPullRequest pr)
    {
        var voters = (pr.Reviewers ?? []).Where(r => r.Vote == -5 && r.Id != pr.CreatedBy?.Id).Select(r => r.Id).OrderBy(i => i).ToList();
        return voters.Count == 0 ? null : new ChangedItem(DateTimeOffset.UtcNow,
            PrUrl: $"{repoUrl}/pullrequest/{pr.PullRequestId}", HeadRef: pr.SourceRefName, SameRepository: pr.ForkSource is null,
            Dedupe: $"{pr.PullRequestId}|{string.Join(',', voters)}");
    }
}
