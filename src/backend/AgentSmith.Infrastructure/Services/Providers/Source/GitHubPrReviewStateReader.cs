using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-13-9a59b: a GitHub pull request's state and its standing decisions in one GraphQL query.
/// latestReviews holds one review per reviewer, so a dismissed review or a later comment-only
/// review leaves no decision. The pull request's author, the token's account (viewer) and bots
/// are excluded. The decision's host id is the review's node id, which the thread reader gives the
/// review body's note.
/// </summary>
public sealed class GitHubPrReviewStateReader(IGitHubClient client, string owner, string repo, string repoUrl) : IPrReviewStateReader
{
    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!){viewer{login} repository(owner:$owner,name:$repo){pullRequest(number:$number){"
        + "number state createdAt mergedAt closedAt author{login} "
        + "latestReviews(first:100){nodes{id state body author{__typename login} authorAssociation submittedAt}}}}}";

    public async Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var response = await client.Connection.Post<string>(new Uri("graphql", UriKind.Relative),
            new { query = Query, variables = new { owner, repo, number } }, "application/json", "application/json");
        using var doc = JsonDocument.Parse(response.Body);
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            throw new InvalidOperationException($"GitHub GraphQL refused the pull-request state query: {errors.GetRawText()}");
        var data = doc.RootElement.GetProperty("data");
        var viewer = data.TryGetProperty("viewer", out var v) && v.ValueKind == JsonValueKind.Object ? Str(v, "login") : null;
        var pr = data.GetProperty("repository").GetProperty("pullRequest");
        var author = pr.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? Str(a, "login") : null;
        var state = Str(pr, "state") switch { "MERGED" => PrReviewStates.Merged, "CLOSED" => PrReviewStates.Closed, _ => PrReviewStates.Open };
        var at = Str(pr, "mergedAt") ?? Str(pr, "closedAt") ?? Str(pr, "createdAt");
        return new PrReviewState(number.ToString(CultureInfo.InvariantCulture), state,
            at is null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(at, CultureInfo.InvariantCulture),
            [.. pr.GetProperty("latestReviews").GetProperty("nodes").EnumerateArray()
                .Select(Decision).OfType<PrReviewDecision>()
                .Where(d => !d.IsBot && d.Author!.AuthorId != author && !string.Equals(d.Author.AuthorId, viewer, StringComparison.OrdinalIgnoreCase))]);
    }

    private PrReviewDecision? Decision(JsonElement review)
    {
        var decision = Str(review, "state") switch
        {
            "APPROVED" => PrReviewDecisions.Approved,
            "CHANGES_REQUESTED" => PrReviewDecisions.ChangesRequested,
            _ => null,
        };
        if (decision is null || Str(review, "submittedAt") is not { } at) return null;
        if (!review.TryGetProperty("author", out var author) || author.ValueKind != JsonValueKind.Object) return null;
        if (Str(author, "login") is not { } login) return null;
        return new PrReviewDecision(Str(review, "id") ?? string.Empty,
            new PrCommentAuthor(repoUrl, $"{owner}/{repo}", login, login) { Association = Str(review, "authorAssociation") },
            Str(author, "__typename") == "Bot", decision, DateTimeOffset.Parse(at, CultureInfo.InvariantCulture), Str(review, "body") ?? string.Empty);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
