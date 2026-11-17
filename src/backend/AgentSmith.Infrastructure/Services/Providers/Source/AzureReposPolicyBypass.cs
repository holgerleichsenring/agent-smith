using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-023fa: completes an Azure Repos pull request NOW, past its branch policies — the operator's call that
/// policies and required reviewers are for people, not for a run whose own gate verified the change. The update
/// carries the last merge source commit Azure requires to complete, BypassPolicy with a reason, and deletes the source
/// branch as the armed completion does. Azure merges asynchronously, so an answer still active with a queued merge is
/// armed rather than refused. A missing permission is refused with the permission and the key named.
/// </summary>
public sealed class AzureReposPolicyBypass(string project, string repoName, ILogger logger)
{
    public const string Policy = "branch policies";
    public const string Permission = "the token's identity 'Bypass policies when completing pull requests' on the repository";
    public const string Reason = "Merged by agent-smith: the run's own gate verified this change green (merge: on_gate_green).";

    public async Task<PullRequestCompletion> CompleteAsync(
        GitHttpClient client, GitPullRequest pr, int prId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(pr);
        try
        {
            var updated = await client.UpdatePullRequestAsync(
                Completing(pr), project, repoName, prId, cancellationToken: cancellationToken);
            return Interpret(updated, prId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Completing PR !{PrId} past its policies was refused", prId);
            return BypassRefusal.Of(Policy, "Azure Repos", ex.Message, Permission);
        }
    }

    /// <summary>The update that completes past the policies.</summary>
    public static GitPullRequest Completing(GitPullRequest pr) => new()
    {
        Status = PullRequestStatus.Completed,
        LastMergeSourceCommit = pr.LastMergeSourceCommit,
        CompletionOptions = new GitPullRequestCompletionOptions
        {
            BypassPolicy = true, BypassReason = Reason, DeleteSourceBranch = true,
        },
    };

    /// <summary>What Azure's answer to the completing update means.</summary>
    public static PullRequestCompletion Interpret(GitPullRequest updated, int prId)
    {
        ArgumentNullException.ThrowIfNull(updated);
        if (updated.Status == PullRequestStatus.Completed) return PullRequestCompletion.Merged();
        if (updated.MergeStatus == PullRequestAsyncStatus.Conflicts)
            return PullRequestCompletion.Refused(updated.MergeFailureMessage ?? $"Pull request !{prId} has merge conflicts.");
        if (updated.MergeStatus is PullRequestAsyncStatus.Failure or PullRequestAsyncStatus.RejectedByPolicy)
            return BypassRefusal.Of(Policy, "Azure Repos",
                updated.MergeFailureMessage ?? $"the merge of !{prId} ended '{updated.MergeStatus}'", Permission);
        return PullRequestCompletion.Armed($"Azure Repos accepted the completion of !{prId} and is merging it.");
    }
}
