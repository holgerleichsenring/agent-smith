using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435b: approves a GitHub pull request only when its reviewDecision says a review is required, with an
/// APPROVE review as the token's account. GitHub refuses an author approving its own pull request; that refusal comes
/// back with GitHub's words.
/// </summary>
public sealed class GitHubApprover(IGitHubClient client, GitHubRepoAddress address)
{
    private const string Query =
        "query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$number){reviewDecision}}}";

    public async Task<PullRequestApproval> ApproveIfRequiredAsync(string prUrl, CancellationToken ct)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            return PullRequestApproval.Refused($"Not a GitHub pull request URL: {prUrl}");
        try
        {
            var data = await new GitHubGraphQlQuery(client, address.Api.GraphQl)
                .RunAsync(Query, new { owner = address.Owner, repo = address.Repo, number }, "review decision");
            var decision = data.GetProperty("repository").GetProperty("pullRequest").GetProperty("reviewDecision");
            if (decision.ValueKind != System.Text.Json.JsonValueKind.String || decision.GetString() != "REVIEW_REQUIRED")
                return PullRequestApproval.NotRequired;
            await client.PullRequest.Review.Create(address.Owner, address.Repo, number, new PullRequestReviewCreate
            {
                Event = PullRequestReviewEvent.Approve, Body = "CI is green; approved by agent-smith (merge: on_ci_green).",
            });
            return PullRequestApproval.Approved;
        }
        catch (Exception ex) when (ex is ApiException or InvalidOperationException) { return PullRequestApproval.Refused(ex.Message); }
    }
}
