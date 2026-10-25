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
/// </summary>
public sealed class GitHubChangedPullRequests(IGitHubClient client, GitHubRepoAddress address)
{
    private const string Query =
        "query($owner:String!,$repo:String!,$after:String){repository(owner:$owner,name:$repo){pullRequests(states:OPEN,first:50,after:$after){"
        + "pageInfo{hasNextPage endCursor} nodes{url headRefName isCrossRepository latestReviews(first:50){nodes{state submittedAt}}}}}}";

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, string? resume, int maxPages, CancellationToken ct)
    {
        var items = new List<ChangedItem>();
        var after = resume;
        for (var page = 0; page < maxPages; page++)
        {
            var response = await client.Connection.Post<string>(address.Api.GraphQl,
                new { query = Query, variables = new { owner = address.Owner, repo = address.Repo, after } }, "application/json", "application/json");
            using var doc = JsonDocument.Parse(response.Body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                throw new InvalidOperationException($"GitHub GraphQL refused the open pull-request query: {errors.GetRawText()}");
            var prs = doc.RootElement.GetProperty("data").GetProperty("repository").GetProperty("pullRequests");
            items.AddRange(prs.GetProperty("nodes").EnumerateArray().Select(n => Item(n, since)).OfType<ChangedItem>());
            var info = prs.GetProperty("pageInfo");
            if (!info.GetProperty("hasNextPage").GetBoolean()) return new ChangedPage(items);
            after = info.GetProperty("endCursor").GetString();
        }
        return new ChangedPage(items, Cut: true, Resume: after);
    }

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
