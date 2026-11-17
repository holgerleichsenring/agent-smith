using AgentSmith.Contracts.Providers;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-713ec: a GitHub repository's agent-smith/ branches, the pull requests from and into each, and the
/// delete — what a branch-cleanup run reads and writes. GitHub's refs listing matches by prefix, so a name that only
/// starts with the prefix is filtered out; a repository with no such ref answers 404, read as none.
/// </summary>
public sealed class GitHubWorkBranches(string owner, string repo, Func<IGitHubClient> client) : IWorkBranchCleanup
{
    public async Task<IReadOnlyList<WorkBranchTip>> ListWorkBranchesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Reference> refs;
        try { refs = await client().Git.Reference.GetAllForSubNamespace(owner, repo, $"heads/{WorkBranches.Prefix}/"); }
        catch (NotFoundException) { return []; }
        return [.. refs.Select(r => new WorkBranchTip(Name(r.Ref), r.Object.Sha)).Where(t => WorkBranches.Owns(t.Name))];
    }

    public async Task<BranchPullRequests> PullRequestsOfAsync(string branch, CancellationToken cancellationToken)
    {
        var github = client();
        var from = await github.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest { State = ItemStateFilter.All, Head = $"{owner}:{branch}" });
        var into = await github.PullRequest.GetAllForRepository(owner, repo,
            new PullRequestRequest { State = ItemStateFilter.Open, Base = branch });
        return new BranchPullRequests([.. from.Select(Of)], into.Count);
    }

    public async Task<bool> DeleteBranchAsync(WorkBranchTip branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(branch);
        await client().Git.Reference.Delete(owner, repo, $"heads/{branch.Name}");
        return true;
    }

    private static BranchPullRequest Of(PullRequest pr) => new(
        pr.State.Value == ItemState.Open ? BranchPullRequestState.Open
            : pr.Merged ? BranchPullRequestState.Merged : BranchPullRequestState.Closed,
        pr.Head?.Sha);

    private static string Name(string reference) =>
        reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..] : reference;
}
