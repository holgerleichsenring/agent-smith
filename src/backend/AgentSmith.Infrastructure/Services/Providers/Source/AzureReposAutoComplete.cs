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
/// The mechanism that works is to ARM: set <c>AutoCompleteSetBy</c>, and the pull request
/// merges itself once the policies pass.
/// </para>
/// <para>
/// 2026-11-25-9992h: armed as the token's own identity — the machine's — never as the pull
/// request's creator, who may be a person from before the machine; and with no vote: a
/// policy that wants a reviewer's approval waits for a person's.
/// </para>
/// </summary>
public sealed class AzureReposAutoComplete(
    string project, string repoName, ILogger logger)
{
    /// <summary>
    /// Arms auto-complete on <paramref name="pr"/> as <paramref name="self"/>. Returns merged when
    /// Azure completed it there and then (no policy stood in the way), armed when it
    /// accepted the instruction and is waiting, and refused when it declined.
    /// </summary>
    public async Task<PullRequestCompletion> ArmAsync(
        GitHttpClient client, GitPullRequest pr, int prId, string? self, MergeStyle style, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(pr);
        if (Self(self) is not { } identity) return Unknown(prId);
        // 2026-11-25-9992i: a squash style arms with mergeStrategy squash.
        var options = style == MergeStyle.Squash
            ? new GitPullRequestCompletionOptions { DeleteSourceBranch = true, MergeStrategy = GitPullRequestMergeStrategy.Squash } : null;
        var updated = await client.UpdatePullRequestAsync(
            Arming(pr, identity, options), project, repoName, prId, cancellationToken: cancellationToken);
        return Interpret(updated, prId);
    }

    private static Microsoft.VisualStudio.Services.WebApi.IdentityRef? Self(string? self) =>
        string.IsNullOrWhiteSpace(self) ? null : new Microsoft.VisualStudio.Services.WebApi.IdentityRef { Id = self };

    private static PullRequestCompletion Unknown(int prId) => PullRequestCompletion.Refused(
        $"Azure DevOps did not say who the token is, so auto-complete on pull request !{prId} is not armed on anyone's behalf.");

    private static GitPullRequest Arming(GitPullRequest pr, Microsoft.VisualStudio.Services.WebApi.IdentityRef identity,
        GitPullRequestCompletionOptions? options = null) => new()
        {
            AutoCompleteSetBy = identity,
            // 2026-11-17-713eb: the merge deletes the source branch. p0490 kept it as an audit
            // trail; the merged pull request is that trail, and a kept branch only went stale.
            CompletionOptions = options ?? new GitPullRequestCompletionOptions { DeleteSourceBranch = true },
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
