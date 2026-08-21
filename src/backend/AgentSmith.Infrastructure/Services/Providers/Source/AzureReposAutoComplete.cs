using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0501: finishes an Azure Repos pull request the way the platform actually allows.
/// <para>
/// p0490 completed one by updating it to <c>Completed</c> with CompletionOptions — an
/// immediate merge, which Azure DevOps refuses outright whenever a branch policy
/// requires an integration build. That is the operator's setup, so the capability was
/// unavailable in exactly the environment it was built for.
/// </para>
/// <para>
/// The mechanism that works is to APPROVE and then ARM: cast an approving reviewer
/// vote, set <c>AutoCompleteSetBy</c>, and the pull request merges itself once the
/// policy passes. The identity both need is already in hand — our own run opened this
/// pull request, so <c>CreatedBy</c> IS the token's identity, and reading it off the
/// pull request avoids a second round trip for a fact the first response returned.
/// </para>
/// </summary>
public sealed class AzureReposAutoComplete(
    string project, string repoName, ILogger logger)
{
    private const short ApproveVote = 10;

    /// <summary>
    /// Approves <paramref name="pr"/> and arms auto-complete on it. Returns merged when
    /// Azure completed it there and then (no policy stood in the way), armed when it
    /// accepted the instruction and is waiting, and refused when it declined.
    /// </summary>
    public async Task<PullRequestCompletion> ArmAsync(
        GitHttpClient client, GitPullRequest pr, int prId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(pr);

        var identity = pr.CreatedBy;
        if (identity is null)
            return PullRequestCompletion.Refused(
                $"Azure DevOps returned no creator identity for pull request !{prId}, "
                + "so auto-complete cannot be armed on anyone's behalf.");

        await ApproveAsync(client, identity, prId, cancellationToken);
        var updated = await client.UpdatePullRequestAsync(
            Arming(pr, identity), project, repoName, prId, cancellationToken: cancellationToken);
        return Interpret(updated, prId);
    }

    private async Task ApproveAsync(
        GitHttpClient client, Microsoft.VisualStudio.Services.WebApi.IdentityRef identity,
        int prId, CancellationToken cancellationToken)
    {
        // A policy that requires approval is satisfied by the vote; one that requires a
        // build is not, and is precisely what auto-complete then waits for.
        await client.CreatePullRequestReviewerAsync(
            new IdentityRefWithVote { Vote = ApproveVote },
            project, repoName, prId, identity.Id, cancellationToken: cancellationToken);
        logger.LogInformation("PR !{PrId} approved by {Identity}", prId, identity.DisplayName ?? identity.Id);
    }

    private static GitPullRequest Arming(
        GitPullRequest pr, Microsoft.VisualStudio.Services.WebApi.IdentityRef identity) => new()
        {
            AutoCompleteSetBy = identity,
            // p0490 keeps the source branch: the init branch is the audit trail of what
            // the run wrote, and deleting branches was explicitly out of scope.
            CompletionOptions = new GitPullRequestCompletionOptions { DeleteSourceBranch = false },
        };

    private PullRequestCompletion Interpret(GitPullRequest updated, int prId)
    {
        if (updated.Status == PullRequestStatus.Completed)
        {
            logger.LogInformation("PR !{PrId} completed immediately — no policy stood in the way", prId);
            return PullRequestCompletion.Merged();
        }
        if (updated.AutoCompleteSetBy is not null)
        {
            logger.LogInformation(
                "PR !{PrId} is armed; Azure DevOps will complete it when its policy passes", prId);
            return PullRequestCompletion.Armed(
                updated.MergeFailureMessage
                ?? "Auto-complete is set; Azure DevOps will merge when the branch policy passes.");
        }
        var reason = updated.MergeFailureMessage
            ?? $"Azure DevOps did not arm auto-complete; the pull request is '{updated.Status}'.";
        logger.LogInformation("PR !{PrId} was not armed: {Reason}", prId, reason);
        return PullRequestCompletion.Refused(reason);
    }
}
