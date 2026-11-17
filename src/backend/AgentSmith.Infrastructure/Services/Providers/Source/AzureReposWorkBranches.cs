using AgentSmith.Contracts.Providers;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-713ec: an Azure Repos repository's agent-smith/ branches, the pull requests from and into each, and
/// the delete — an update of the ref to the zero id with the tip as its old id, so Azure refuses to delete a branch
/// that moved since it was listed.
/// </summary>
public sealed class AzureReposWorkBranches(
    string project, string repoName, Func<CancellationToken, Task<GitHttpClient>> client) : IWorkBranchCleanup
{
    private const string Heads = "refs/heads/";
    private const string ZeroId = "0000000000000000000000000000000000000000";

    public async Task<IReadOnlyList<WorkBranchTip>> ListWorkBranchesAsync(CancellationToken cancellationToken)
    {
        var refs = await (await client(cancellationToken)).GetRefsAsync(
            project, repoName, filter: $"heads/{WorkBranches.Prefix}/", cancellationToken: cancellationToken);
        return [.. (refs ?? []).Select(r => new WorkBranchTip(Name(r.Name), r.ObjectId)).Where(t => WorkBranches.Owns(t.Name))];
    }

    public async Task<BranchPullRequests> PullRequestsOfAsync(string branch, CancellationToken cancellationToken)
    {
        var git = await client(cancellationToken);
        var from = await git.GetPullRequestsAsync(project, repoName,
            new GitPullRequestSearchCriteria { SourceRefName = Heads + branch, Status = PullRequestStatus.All },
            cancellationToken: cancellationToken);
        var into = await git.GetPullRequestsAsync(project, repoName,
            new GitPullRequestSearchCriteria { TargetRefName = Heads + branch, Status = PullRequestStatus.Active },
            cancellationToken: cancellationToken);
        return new BranchPullRequests([.. (from ?? []).Select(Of)], (into ?? []).Count);
    }

    public async Task<bool> DeleteBranchAsync(WorkBranchTip branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var results = await (await client(cancellationToken)).UpdateRefsAsync(
            [new GitRefUpdate { Name = Heads + branch.Name, OldObjectId = branch.Sha, NewObjectId = ZeroId }],
            project, repoName, projectId: null, cancellationToken: cancellationToken);
        return results is { Count: > 0 } && results.All(r => r.Success);
    }

    private static BranchPullRequest Of(GitPullRequest pr) => new(
        pr.Status switch
        {
            PullRequestStatus.Completed => BranchPullRequestState.Merged,
            PullRequestStatus.Abandoned => BranchPullRequestState.Closed,
            _ => BranchPullRequestState.Open,
        },
        pr.LastMergeSourceCommit?.CommitId);

    private static string Name(string reference) =>
        reference.StartsWith(Heads, StringComparison.Ordinal) ? reference[Heads.Length..] : reference;
}
