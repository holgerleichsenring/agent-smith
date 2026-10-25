using System.Text.Json;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbbf: reads every review thread, review and comment of a GitHub pull request, and every
/// comment of each thread — 100 a page, one cursor per connection, until a connection says it has no
/// next page — so the newest feedback is never the part cut off.
/// </summary>
public sealed class GitHubPrReviewPager(GitHubGraphQlQuery graphQl, GitHubRepoAddress address)
{
    public async Task<GitHubPrReviewNodes> ReadAsync(int number)
    {
        var data = await graphQl.RunAsync(GitHubPrReviewQueries.First, Variables(number, null), "review");
        var pr = PullRequest(data);
        var threads = await AllAsync(pr.GetProperty("reviewThreads"), number, GitHubPrReviewQueries.Threads, "reviewThreads");
        var reviews = await AllAsync(pr.GetProperty("reviews"), number, GitHubPrReviewQueries.Reviews, "reviews");
        var comments = await AllAsync(pr.GetProperty("comments"), number, GitHubPrReviewQueries.Comments, "comments");
        var full = new List<(JsonElement, IReadOnlyList<JsonElement>)>();
        foreach (var thread in threads)
            full.Add((thread, await ThreadCommentsAsync(thread)));
        var viewer = data.TryGetProperty("viewer", out var v) && v.ValueKind == JsonValueKind.Object ? Str(v, "login") : null;
        return new GitHubPrReviewNodes(viewer, full, reviews, comments);
    }

    private Task<IReadOnlyList<JsonElement>> AllAsync(JsonElement first, int number, string query, string field) =>
        PagesAsync(first, async cursor =>
            PullRequest(await graphQl.RunAsync(query, Variables(number, cursor), field)).GetProperty(field));

    private Task<IReadOnlyList<JsonElement>> ThreadCommentsAsync(JsonElement thread) =>
        PagesAsync(thread.GetProperty("comments"), async cursor =>
            (await graphQl.RunAsync(GitHubPrReviewQueries.ThreadComments, new { id = Str(thread, "id"), after = cursor },
                "thread comments")).GetProperty("node").GetProperty("comments"));

    private static async Task<IReadOnlyList<JsonElement>> PagesAsync(JsonElement page, Func<string, Task<JsonElement>> next)
    {
        var nodes = new List<JsonElement>(Nodes(page));
        while (NextCursor(page) is { } cursor)
        {
            page = await next(cursor);
            nodes.AddRange(Nodes(page));
        }
        return nodes;
    }

    private object Variables(int number, string? after) =>
        new { owner = address.Owner, repo = address.Repo, number, after };

    private static JsonElement PullRequest(JsonElement data) => data.GetProperty("repository").GetProperty("pullRequest");

    private static string? NextCursor(JsonElement connection) =>
        connection.TryGetProperty("pageInfo", out var info) && info.TryGetProperty("hasNextPage", out var more)
        && more.ValueKind == JsonValueKind.True ? Str(info, "endCursor") : null;

    private static IEnumerable<JsonElement> Nodes(JsonElement connection) =>
        connection.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array ? nodes.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
