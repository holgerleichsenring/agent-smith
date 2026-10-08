using AgentSmith.Contracts.Sweep;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-9e6e: GitHub issues updated since a cursor, oldest first (since, sort=updated,
/// direction=asc). Pull requests come back as issues too; they are skipped — the PR source reads them.
/// </summary>
public sealed class GitHubChangedTickets(IGitHubClient client, string owner, string repo)
{
    private const int PageSize = 100;

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var request = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All, Since = since, SortProperty = IssueSort.Updated, SortDirection = SortDirection.Ascending,
        };
        var issues = await client.Issue.GetAllForRepository(owner, repo, request,
            new ApiOptions { PageSize = PageSize, PageCount = maxPages, StartPage = 1 });
        var items = issues.Where(i => i.PullRequest is null)
            .Select(i => new ChangedItem(i.UpdatedAt ?? i.CreatedAt, TicketId: i.Number.ToString())).ToList();
        return new ChangedPage(items, Cut: issues.Count >= PageSize * maxPages);
    }
}
