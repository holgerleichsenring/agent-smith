using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: changes the state of an already-opened GitHub pull request, addressed by the
/// web URL <c>CreatePullRequestAsync</c> handed back — its body, its draft flag, or
/// whether it is merged. Opening one needs a repository, a branch and a default-branch
/// lookup; changing one that exists needs only the number recovered from that URL,
/// which is why the three edits share this type and its single parser.
/// </summary>
public sealed class GitHubPullRequestUpdater(
    string owner, string repo, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    public async Task<bool> UpdateBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullNumber(prUrl, out var prNumber)) return false;
        _ = cancellationToken;
        try
        {
            await Client().PullRequest.Update(
                owner, repo, prNumber, new PullRequestUpdate { Body = newBody });
            logger.LogInformation("Updated PR body for #{Pr}", prNumber);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update PR body for #{Pr}", prNumber);
            return false;
        }
    }

    // p0393a: GitHub's REST PR update cannot take a pull request out of draft — only the
    // GraphQL markPullRequestReadyForReview mutation can, and it needs the PR's node id,
    // which the REST read hands us.
    public async Task<bool> MarkReadyAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParsePullNumber(prUrl, out var prNumber)) return false;
        _ = cancellationToken;
        try
        {
            var client = Client();
            var pr = await client.PullRequest.Get(owner, repo, prNumber);
            if (!pr.Draft) return true;
            return await MarkReadyViaGraphQlAsync(client, pr.NodeId, prNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR #{Pr} ready for review", prNumber);
            return false;
        }
    }

    // p0490: branch protection answers a merge that may not happen with 405
    // (PullRequestNotMergeableException) and a head that moved with 409
    // (PullRequestMismatchException). Both are the platform declining, and both carry
    // the sentence the operator needs, so every failure is reported as a refusal
    // instead of thrown at a run that has already done its work.
    public async Task<PullRequestCompletion> CompleteAsync(
        string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParsePullNumber(prUrl, out var prNumber))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitHub pull request URL.");
        _ = cancellationToken;
        try
        {
            var merge = await Client().PullRequest.Merge(owner, repo, prNumber, new MergePullRequest());
            if (merge.Merged)
            {
                logger.LogInformation("PR #{Pr} merged", prNumber);
                return PullRequestCompletion.Merged();
            }
            logger.LogInformation("PR #{Pr} was not merged: {Message}", prNumber, merge.Message);
            return PullRequestCompletion.Refused(merge.Message ?? "GitHub declined the merge.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Merging PR #{Pr} was refused", prNumber);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    // The mutation goes over the SAME authenticated Octokit connection as every other
    // call — a second HttpClient here would be a second credential path and a second
    // socket pool for one request.
    private async Task<bool> MarkReadyViaGraphQlAsync(
        IGitHubClient client, string nodeId, int prNumber)
    {
        var body = new
        {
            query = "mutation($id:ID!){markPullRequestReadyForReview(input:{pullRequestId:$id})"
                + "{pullRequest{isDraft}}}",
            variables = new { id = nodeId },
        };
        var response = await client.Connection.Post<string>(
            new Uri("graphql", UriKind.Relative), body, "application/json", "application/json");
        if ((int)response.HttpResponse.StatusCode >= 300)
        {
            logger.LogWarning(
                "Marking PR #{Pr} ready returned {Status}",
                prNumber, (int)response.HttpResponse.StatusCode);
            return false;
        }
        logger.LogInformation("PR #{Pr} is out of draft — the sequence completed", prNumber);
        return true;
    }

    private IGitHubClient Client() => clientFactory.Create(token);

    private static bool TryParsePullNumber(string prUrl, out int prNumber)
    {
        prNumber = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pull/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prNumber);
    }
}
