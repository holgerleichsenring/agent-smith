using System.Globalization;
using System.Text.Json;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92d: hides the pull request's comments the token wrote that start with a marker — a notice agent-smith posted that has
/// stopped being true — as RESOLVED (minimizeComment: "the content has been resolved"). GitHub cannot resolve an issue comment, and none blocks a merge, so this is the
/// notice going quiet rather than a merge unblocked. A comment someone else wrote is left (viewerDidAuthor), since a marker is text
/// anyone can paste; one GitHub refuses leaves the rest. Every page of comments is read.
/// </summary>
public sealed class GitHubMarkedComments(IGitHubClient client, GitHubRepoAddress address, ILogger logger)
{
    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!,$after:String){repository(owner:$owner,name:$repo){pullRequest(number:$number){"
        + "comments(first:100,after:$after){pageInfo{hasNextPage endCursor} nodes{id body isMinimized viewerDidAuthor}}}}}";

    private const string Minimize =
        "mutation($id:ID!){minimizeComment(input:{subjectId:$id,classifier:RESOLVED}){minimizedComment{isMinimized}}}";

    public async Task<int> MinimizeAsync(string prIdentifier, string markerPrefix, CancellationToken ct)
    {
        var number = int.Parse(prIdentifier, CultureInfo.InvariantCulture);
        var graphQl = new GitHubGraphQlQuery(client, address.Api.GraphQl);
        var hidden = 0;
        foreach (var id in await MarkedAsync(graphQl, number, markerPrefix, ct))
            if (await HideAsync(graphQl, number, id)) hidden++;
        if (hidden > 0) logger.LogInformation("PR #{Pr}: {Count} marked comment(s) hidden as resolved", number, hidden);
        return hidden;
    }

    private async Task<IReadOnlyList<string>> MarkedAsync(GitHubGraphQlQuery graphQl, int number, string marker, CancellationToken ct)
    {
        var ids = new List<string>();
        string? after = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var comments = (await graphQl.RunAsync(Query, new { owner = address.Owner, repo = address.Repo, number, after }, "pull-request comments"))
                .GetProperty("repository").GetProperty("pullRequest").GetProperty("comments");
            ids.AddRange(comments.GetProperty("nodes").EnumerateArray().Where(c => IsOpenMarked(c, marker)).Select(c => c.GetProperty("id").GetString()!));
            var page = comments.GetProperty("pageInfo");
            after = page.GetProperty("hasNextPage").GetBoolean() ? page.GetProperty("endCursor").GetString() : null;
        }
        while (after is not null);
        return ids;
    }

    private async Task<bool> HideAsync(GitHubGraphQlQuery graphQl, int number, string id)
    {
        try
        {
            await graphQl.RunAsync(Minimize, new { id }, "minimizeComment");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ApiException)
        {
            logger.LogWarning(ex, "PR #{Pr}: marked comment {Id} was not hidden", number, id);
            return false;
        }
    }

    private static bool IsOpenMarked(JsonElement comment, string marker) =>
        comment.TryGetProperty("viewerDidAuthor", out var mine) && mine.ValueKind == JsonValueKind.True
        && !(comment.TryGetProperty("isMinimized", out var hidden) && hidden.ValueKind == JsonValueKind.True)
        && comment.TryGetProperty("body", out var body) && body.GetString()?.StartsWith(marker, StringComparison.Ordinal) == true;
}
