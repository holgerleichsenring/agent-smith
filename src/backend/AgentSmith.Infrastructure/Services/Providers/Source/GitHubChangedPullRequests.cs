using System.Text.Json;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-9e6e: a GitHub repository's open pull requests whose latest review by someone was
/// submitted after the cursor — read from latestReviews, so a body-only review counts without
/// depending on the PR's updated time. An item is an act when such a review is CHANGES_REQUESTED,
/// otherwise RefreshOnly (an approval or a comment review only changes the record). A dismissal keeps
/// its review's submittedAt, so it is not listed. Paged by endCursor within the budget; a cut read
/// resumes at the page it stopped. A reply carrying an errors array throws.
/// <para>2026-11-25-cc1f: every open pull request, reviewed or not, is also listed RefreshOnly under a name of its base, head, draft
/// and mergeable state and its reviews (<see cref="PullRequestRefreshName"/>) — a retarget, a push, a draft lift or a new conflict
/// re-reads its record once. Such an item carries the read's since, so it never moves the cursor.</para>
/// </summary>
public sealed class GitHubChangedPullRequests(IGitHubClient client, GitHubRepoAddress address)
{
    private const string Query =
        "query($owner:String!,$repo:String!,$after:String){repository(owner:$owner,name:$repo){pullRequests(states:OPEN,first:50,after:$after){"
        + "pageInfo{hasNextPage endCursor} nodes{url headRefName headRefOid baseRefName isDraft mergeable isCrossRepository "
        + "latestReviews(first:50){nodes{state submittedAt}}}}}}";

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, string? resume, int maxPages, CancellationToken ct)
    {
        var items = new List<ChangedItem>();
        var graphQl = new GitHubGraphQlQuery(client, address.Api.GraphQl);
        var after = resume;
        for (var page = 0; page < maxPages; page++)
        {
            var data = await graphQl.RunAsync(Query, new { owner = address.Owner, repo = address.Repo, after }, "open pull-request");
            var prs = data.GetProperty("repository").GetProperty("pullRequests");
            foreach (var node in prs.GetProperty("nodes").EnumerateArray())
            {
                if (Item(node, since) is { } act) items.Add(act);
                items.Add(Refresh(node, since));
            }
            var info = prs.GetProperty("pageInfo");
            if (!info.GetProperty("hasNextPage").GetBoolean()) return new ChangedPage(items);
            after = info.GetProperty("endCursor").GetString();
        }
        return new ChangedPage(items, Cut: true, Resume: after);
    }

    // 2026-11-25-cc1f: the pull request as it stands, named so any change to it is a new name.
    private static ChangedItem Refresh(JsonElement pr, DateTimeOffset since) => new(since,
        PrUrl: pr.GetProperty("url").GetString(), HeadRef: pr.GetProperty("headRefName").GetString(),
        SameRepository: !pr.GetProperty("isCrossRepository").GetBoolean(), RefreshOnly: true,
        Dedupe: PullRequestRefreshName.Of(Text(pr, "url"), Text(pr, "baseRefName"), Text(pr, "headRefOid"), Text(pr, "isDraft"),
            Text(pr, "mergeable"), pr.GetProperty("latestReviews").GetProperty("nodes").EnumerateArray()
                .Select(r => $"{Text(r, "state")}@{Text(r, "submittedAt")}")));

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) : null;

    private static ChangedItem? Item(JsonElement pr, DateTimeOffset since)
    {
        var reviews = pr.GetProperty("latestReviews").GetProperty("nodes").EnumerateArray()
            .Where(r => r.TryGetProperty("submittedAt", out var s) && s.ValueKind == JsonValueKind.String)
            .Select(r => (State: r.GetProperty("state").GetString(), At: DateTimeOffset.Parse(r.GetProperty("submittedAt").GetString()!)))
            .Where(r => r.At > since).ToList();
        return reviews.Count > 0
            ? new ChangedItem(reviews.Max(r => r.At), PrUrl: pr.GetProperty("url").GetString(), HeadRef: pr.GetProperty("headRefName").GetString(),
                SameRepository: !pr.GetProperty("isCrossRepository").GetBoolean(),
                RefreshOnly: reviews.All(r => r.State != "CHANGES_REQUESTED"))
            : null;
    }
}
