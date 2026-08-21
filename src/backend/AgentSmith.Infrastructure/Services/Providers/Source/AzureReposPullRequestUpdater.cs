using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: changes the state of an already-opened Azure Repos pull request, addressed
/// by the web URL <c>CreatePullRequestAsync</c> handed back — its description, its draft
/// flag, or whether it is completed. Opening one needs a repository, a branch and a
/// default-branch lookup; changing one that exists needs only the id recovered from that
/// URL, which is why the three edits share this type and its single parser.
/// </summary>
public sealed class AzureReposPullRequestUpdater(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    public async Task<bool> UpdateBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            await UpdateAsync(
                new GitPullRequest
                {
                    Description = AzureReposSourceProvider.TruncateDescription(newBody),
                },
                prId, cancellationToken);
            logger.LogInformation("Updated PR body for !{PrId}", prId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to update PR body for !{PrId} ({Length} chars, limit {Limit})",
                prId, newBody?.Length ?? 0, AzureReposSourceProvider.MaxDescriptionChars);
            return false;
        }
    }

    // p0393a: Azure DevOps takes a pull request out of draft with the same update call
    // that sets the description.
    public async Task<bool> MarkReadyAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            await UpdateAsync(new GitPullRequest { IsDraft = false }, prId, cancellationToken);
            logger.LogInformation("PR !{PrId} is out of draft — the sequence completed", prId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR !{PrId} ready for review", prId);
            return false;
        }
    }

    // p0490: Azure DevOps completes a pull request by updating it to Completed against
    // the head it last merged — the LastMergeSourceCommit read here is what makes the
    // completion refuse instead of racing a branch that moved. A branch policy that is
    // not satisfied leaves the pull request Active and names itself in
    // MergeFailureMessage, which is the reason recorded for the repo.
    public async Task<PullRequestCompletion> CompleteAsync(
        string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId))
            return PullRequestCompletion.Refused($"'{prUrl}' is not an Azure Repos pull request URL.");
        try
        {
            var client = CreateGitClient();
            var pr = await client.GetPullRequestAsync(
                project, repoName, prId, cancellationToken: cancellationToken);
            var completed = await UpdateAsync(Completion(pr), prId, cancellationToken);
            if (completed.Status == PullRequestStatus.Completed)
            {
                logger.LogInformation("PR !{PrId} completed", prId);
                return PullRequestCompletion.Merged();
            }
            return Refuse(prId, completed.MergeFailureMessage
                ?? $"Azure DevOps left the pull request in status '{completed.Status}'.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Completing PR !{PrId} was refused", prId);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    private static GitPullRequest Completion(GitPullRequest pr) => new()
    {
        Status = PullRequestStatus.Completed,
        LastMergeSourceCommit = pr.LastMergeSourceCommit,
        // p0490 keeps the source branch: the init branch is the audit trail of what
        // the run wrote, and deleting branches was explicitly out of scope.
        CompletionOptions = new GitPullRequestCompletionOptions { DeleteSourceBranch = false },
    };

    private PullRequestCompletion Refuse(int prId, string reason)
    {
        logger.LogInformation("PR !{PrId} was not completed: {Reason}", prId, reason);
        return PullRequestCompletion.Refused(reason);
    }

    private Task<GitPullRequest> UpdateAsync(
        GitPullRequest update, int prId, CancellationToken cancellationToken) =>
        CreateGitClient().UpdatePullRequestAsync(
            update, project, repoName, prId, cancellationToken: cancellationToken);

    private GitHttpClient CreateGitClient() =>
        clientFactory.CreateGitClient(organizationUrl, personalAccessToken);

    private static bool TryParsePullRequestId(string prUrl, out int prId)
    {
        prId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pullrequest/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prId);
    }
}
