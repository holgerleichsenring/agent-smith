using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d: a GitHub pull request's review, read over GraphQL on the provider's own Octokit
/// connection — thread resolution exists only in GraphQL. Review threads, review bodies (an empty
/// body, like the COMMENT review our pr-review posts, is no note) and top-level comments.
/// <para>2026-10-08-f147: the same query asks who the token is (viewer), so a marked note is ours
/// only when the token's account wrote it.</para>
/// <para>2026-10-13-9a59b: every note and thread carries its node id; a review body's thread is FromReview.</para>
/// <para>2026-10-25-9dbbf: every page of every connection is read (<see cref="GitHubPrReviewPager"/>).</para>
/// </summary>
public sealed class GitHubPrReviewThreadReader(
    IGitHubClient client, GitHubRepoAddress address, ILogger logger) : IPrReviewThreadReader
{
    private string? _viewer;

    public async Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var nodes = await new GitHubPrReviewPager(new GitHubGraphQlQuery(client, address.Api.GraphQl), address).ReadAsync(number);
        _viewer = nodes.Viewer;
        logger.LogDebug("PR #{Pr}: {Threads} review thread(s), {Reviews} review(s), {Comments} comment(s) read",
            number, nodes.Threads.Count, nodes.Reviews.Count, nodes.Comments.Count);
        return [.. nodes.Threads.Select(t => InlineThread(t.Thread, t.Comments)),
            .. nodes.Reviews.Select(n => Single(n, "submittedAt", fromReview: true)).OfType<PrReviewThread>(),
            .. nodes.Comments.Select(n => Single(n, "createdAt", fromReview: false)).OfType<PrReviewThread>()];
    }

    private PrReviewThread InlineThread(JsonElement node, IReadOnlyList<JsonElement> comments)
    {
        var line = Int(node, "line") ?? Int(node, "originalLine");
        var notes = comments.Select(c => Note(c, "publishedAt", "createdAt")).OfType<PrReviewNote>();
        return new PrReviewThread(Str(node, "path"), line, node.GetProperty("isResolved").GetBoolean(), [.. notes])
            { HostId = Str(node, "id") ?? string.Empty };
    }

    private PrReviewThread? Single(JsonElement node, string timeField, bool fromReview) =>
        Note(node, timeField, timeField) is { } note
            ? new PrReviewThread(null, null, null, [note]) { HostId = note.HostId, FromReview = fromReview }
            : null;

    private PrReviewNote? Note(JsonElement node, string timeField, string fallbackTime)
    {
        var body = Str(node, "body");
        if (string.IsNullOrWhiteSpace(body)) return null;
        var at = Str(node, timeField) ?? Str(node, fallbackTime);
        if (at is null) return null;
        var author = node.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;
        var login = author is { } l ? Str(l, "login") : null;
        return new PrReviewNote(Author(author),
            author is { } x && Str(x, "__typename") == "Bot", false, DateTimeOffset.Parse(at), body)
        {
            IsOurs = OwnPrNoteMarker.IsOurs(body, login is not null && string.Equals(login, _viewer, StringComparison.OrdinalIgnoreCase)),
            HostId = Str(node, "id") ?? string.Empty,
        };
    }

    private PrCommentAuthor? Author(JsonElement? author) =>
        author is { } a && Str(a, "login") is { } login
            ? new PrCommentAuthor(address.RepoUrl, address.FullName, login, login)
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
