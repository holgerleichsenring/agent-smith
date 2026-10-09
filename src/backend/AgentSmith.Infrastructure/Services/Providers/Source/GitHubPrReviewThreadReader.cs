using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d: a GitHub pull request's review, read in one GraphQL query over the provider's
/// own Octokit connection — thread resolution exists only in GraphQL. Review threads, review bodies
/// (an empty body, like the COMMENT review our pr-review posts, is no note) and top-level comments,
/// 100 of each; a reply carrying an errors array throws, because GraphQL fails as HTTP 200.
/// <para>2026-10-08-f147: the same query asks who the token is (viewer), so a marked note is ours
/// only when the token's account wrote it.</para>
/// </summary>
public sealed class GitHubPrReviewThreadReader(
    IGitHubClient client, string owner, string repo, string repoUrl, ILogger logger) : IPrReviewThreadReader
{
    private string? _viewer;

    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!){viewer{login} repository(owner:$owner,name:$repo){pullRequest(number:$number){"
        + "reviewThreads(first:100){pageInfo{hasNextPage} nodes{isResolved path line originalLine "
        + "comments(first:100){nodes{author{__typename login} authorAssociation body createdAt publishedAt}}}}"
        + "reviews(first:100){nodes{author{__typename login} authorAssociation body submittedAt}}"
        + "comments(first:100){nodes{author{__typename login} authorAssociation body createdAt}}}}}";

    public async Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var response = await client.Connection.Post<string>(new Uri("graphql", UriKind.Relative),
            new { query = Query, variables = new { owner, repo, number } }, "application/json", "application/json");
        using var doc = JsonDocument.Parse(response.Body);
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            throw new InvalidOperationException($"GitHub GraphQL refused the review query: {errors.GetRawText()}");
        var data = doc.RootElement.GetProperty("data");
        _viewer = data.TryGetProperty("viewer", out var viewer) && viewer.ValueKind == JsonValueKind.Object ? Str(viewer, "login") : null;
        return Read(data.GetProperty("repository").GetProperty("pullRequest"), number);
    }

    private IReadOnlyList<PrReviewThread> Read(JsonElement pr, int number)
    {
        var threads = pr.GetProperty("reviewThreads");
        if (threads.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())
            logger.LogWarning("PR #{Pr} has more than 100 review threads; the first 100 are read", number);
        return [.. Nodes(threads).Select(InlineThread),
            .. Nodes(pr.GetProperty("reviews")).Select(n => Single(n, "submittedAt")).OfType<PrReviewThread>(),
            .. Nodes(pr.GetProperty("comments")).Select(n => Single(n, "createdAt")).OfType<PrReviewThread>()];
    }

    private PrReviewThread InlineThread(JsonElement node)
    {
        var line = Int(node, "line") ?? Int(node, "originalLine");
        var notes = Nodes(node.GetProperty("comments")).Select(c => Note(c, "publishedAt", "createdAt")).OfType<PrReviewNote>();
        return new PrReviewThread(Str(node, "path"), line, node.GetProperty("isResolved").GetBoolean(), [.. notes]);
    }

    private PrReviewThread? Single(JsonElement node, string timeField) =>
        Note(node, timeField, timeField) is { } note ? new PrReviewThread(null, null, null, [note]) : null;

    private PrReviewNote? Note(JsonElement node, string timeField, string fallbackTime)
    {
        var body = Str(node, "body");
        if (string.IsNullOrWhiteSpace(body)) return null;
        var at = Str(node, timeField) ?? Str(node, fallbackTime);
        if (at is null) return null;
        var author = node.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;
        var login = author is { } l ? Str(l, "login") : null;
        return new PrReviewNote(Author(author, Str(node, "authorAssociation")),
            author is { } x && Str(x, "__typename") == "Bot", false, DateTimeOffset.Parse(at), body)
        {
            IsOurs = OwnPrNoteMarker.IsOurs(body, login is not null && string.Equals(login, _viewer, StringComparison.OrdinalIgnoreCase)),
        };
    }

    private PrCommentAuthor? Author(JsonElement? author, string? association) =>
        author is { } a && Str(a, "login") is { } login
            ? new PrCommentAuthor(repoUrl, $"{owner}/{repo}", login, login) { Association = association }
            : null;

    private static IEnumerable<JsonElement> Nodes(JsonElement connection) =>
        connection.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array ? nodes.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
