using Microsoft.Extensions.Logging;
using AgentSmith.Infrastructure.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: changes the state of an already-opened GitHub pull request, addressed by the
/// web URL <c>CreatePullRequestAsync</c> handed back — its body or its draft flag. Opening one
/// needs a repository, a branch and a default-branch lookup; changing one that exists needs only
/// the number recovered from that URL, which is why the edits share this type and its single
/// parser. Merging is <see cref="GitHubPullRequestMerger"/>'s (2026-10-25-9dbbc).
/// </summary>
public sealed class GitHubPullRequestUpdater(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    public async Task<bool> UpdateBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullNumber(prUrl, out var prNumber)) return false;
        _ = cancellationToken;
        try
        {
            await Client().PullRequest.Update(
                address.Owner, address.Repo, prNumber, new PullRequestUpdate { Body = newBody });
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
            var pr = await client.PullRequest.Get(address.Owner, address.Repo, prNumber);
            if (!pr.Draft) return true;
            return await MarkReadyViaGraphQlAsync(client, pr.NodeId, prNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR #{Pr} ready for review", prNumber);
            return false;
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
            address.Api.GraphQl, body, "application/json", "application/json");
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

    private IGitHubClient Client() => clientFactory.Create(address, token);

    // 2026-09-13-a284: internal so the retarget type reads the SAME url shape, once.
    internal static bool TryParsePullNumber(string prUrl, out int prNumber)
    {
        prNumber = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pull/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prNumber);
    }
}
