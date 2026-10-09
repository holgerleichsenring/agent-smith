using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9c: GitHub's standing requests for changes — latestReviews holds one review per
/// reviewer, so a reviewer who approved after requesting changes no longer counts. Read over the
/// provider's own Octokit connection; a reply carrying an errors array throws.
/// </summary>
public sealed class GitHubPrReviewActReader(IGitHubClient client, string owner, string repo, string repoUrl) : IPrReviewActReader
{
    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$number){"
        + "author{login} latestReviews(first:100){nodes{state author{__typename login} authorAssociation submittedAt}}}}}";

    public async Task<IReadOnlyList<PrReviewNote>> ChangesRequestedAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            throw new ArgumentException($"Not a GitHub pull request URL: {prUrl}", nameof(prUrl));
        var response = await client.Connection.Post<string>(new Uri("graphql", UriKind.Relative),
            new { query = Query, variables = new { owner, repo, number } }, "application/json", "application/json");
        using var doc = JsonDocument.Parse(response.Body);
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            throw new InvalidOperationException($"GitHub GraphQL refused the review-state query: {errors.GetRawText()}");
        var pr = doc.RootElement.GetProperty("data").GetProperty("repository").GetProperty("pullRequest");
        var prAuthor = pr.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? Str(a, "login") : null;
        return [.. pr.GetProperty("latestReviews").GetProperty("nodes").EnumerateArray()
            .Where(n => Str(n, "state") == "CHANGES_REQUESTED" && Str(n, "submittedAt") is not null)
            .Select(Note).OfType<PrReviewNote>().Where(n => n.Author!.AuthorId != prAuthor)];
    }

    private PrReviewNote? Note(JsonElement review)
    {
        if (!review.TryGetProperty("author", out var author) || author.ValueKind != JsonValueKind.Object) return null;
        var login = Str(author, "login");
        if (login is null) return null;
        return new PrReviewNote(
            new PrCommentAuthor(repoUrl, $"{owner}/{repo}", login, login) { Association = Str(review, "authorAssociation") },
            Str(author, "__typename") == "Bot", false, DateTimeOffset.Parse(Str(review, "submittedAt")!), string.Empty);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
