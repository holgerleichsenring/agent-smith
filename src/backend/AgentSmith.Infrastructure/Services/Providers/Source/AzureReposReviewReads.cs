using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d / 2026-10-08-e8b9c: a pull request's review threads and its standing "Wait for
/// author" votes, over the provider's Git client. The repository's ids come with the answer, because
/// an Azure DevOps author's trust verdict is keyed on them. 2026-10-13-9a59b: and its state with the
/// standing votes. 2026-11-24-9dc0c: and, for a clean merge status, the target branch's commit now.
/// 2026-11-26-5201f: and whether a reviewer policy still waits for a person's approval (<see cref="AzureReposApprovalPolicies"/>).
/// </summary>
public sealed class AzureReposReviewReads(
    Func<CancellationToken, Task<GitHttpClient>> connect, Func<CancellationToken, Task<string?>> self,
    string project, string repoName, string repoUrl, ILogger? logger = null, AzureReposPolicyEvaluations? policies = null)
    : IPrReviewThreadReader, IPrReviewActReader, IPrReviewStateReader
{
    public async Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken)
    {
        var prId = PrId(prUrl);
        var client = await connect(cancellationToken);
        var repo = await client.GetRepositoryAsync(project, repoName, cancellationToken: cancellationToken);
        var threads = await client.GetThreadsAsync(project, repoName, prId, cancellationToken: cancellationToken);
        return AzureReposReviewMapping.Map(threads, repoUrl, repo.Id.ToString(), repo.ProjectReference.Id.ToString(),
            await self(cancellationToken));
    }

    public async Task<IReadOnlyList<PrReviewNote>> ChangesRequestedAsync(string prUrl, CancellationToken cancellationToken)
    {
        var prId = PrId(prUrl);
        var client = await connect(cancellationToken);
        var pr = await client.GetPullRequestAsync(project, repoName, prId, cancellationToken: cancellationToken);
        var threads = await client.GetThreadsAsync(project, repoName, prId, cancellationToken: cancellationToken);
        return AzureReposVoteActs.Standing(threads, pr.Reviewers ?? [], repoUrl,
            pr.Repository.Id.ToString(), pr.Repository.ProjectReference.Id.ToString());
    }

    public async Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken)
    {
        var prId = PrId(prUrl);
        var client = await connect(cancellationToken);
        var pr = await client.GetPullRequestAsync(project, repoName, prId, cancellationToken: cancellationToken);
        var threads = await client.GetThreadsAsync(project, repoName, prId, cancellationToken: cancellationToken);
        var tip = pr.Status == PullRequestStatus.Active && pr.MergeStatus == PullRequestAsyncStatus.Succeeded
            ? await TipAsync(client, pr.TargetRefName, cancellationToken) : null;
        return AzureReposVoteDecisions.Read(pr, threads, await self(cancellationToken), repoUrl, tip)
            with { ApprovalRequired = pr.Status == PullRequestStatus.Active ? await ApprovalAsync(pr, prId, cancellationToken) : null };
    }

    // 2026-11-26-5201f: a blocking reviewer policy not approved yet. Evaluations that cannot be read say nothing: the state
    // read itself must not fail on them.
    private async Task<bool?> ApprovalAsync(GitPullRequest pr, int prId, CancellationToken ct)
    {
        if (policies is null || pr.Repository?.ProjectReference?.Id is not { } projectId) return null;
        try
        {
            return AzureReposApprovalPolicies.Required(await policies(projectId, AzureReposCiPolicies.ArtifactId(projectId, prId), ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "Pull request {PrId}: its policy evaluations could not be read; whether it needs an approval is unknown", prId);
            return null;
        }
    }

    // 2026-11-24-9dc0c: the target branch's commit now. The refs filter is relative to refs/ and a PREFIX match
    // (AzureReposBranchRead), so only the exact name counts; a target that is gone answers null.
    // A tip that cannot be read keeps the status as before: the state read itself must not fail on it.
    private async Task<string?> TipAsync(GitHttpClient client, string? targetRef, CancellationToken ct)
    {
        if (targetRef is not { Length: > 5 } || !targetRef.StartsWith("refs/", StringComparison.Ordinal)) return null;
        try
        {
            var refs = await client.GetRefsAsync(project, repoName, filter: targetRef[5..], cancellationToken: ct);
            return refs?.FirstOrDefault(r => string.Equals(r.Name, targetRef, StringComparison.Ordinal))?.ObjectId;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "{Ref}: the target's tip could not be read; the merge status is taken as Azure says it", targetRef);
            return null;
        }
    }

    private static int PrId(string prUrl) => AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId)
        ? prId : throw new ArgumentException($"Not an Azure Repos pull request URL: {prUrl}", nameof(prUrl));
}
