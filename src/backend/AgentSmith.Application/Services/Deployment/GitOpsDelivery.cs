using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: delivers a changed manifest the GitOps way — one commit on a fresh branch cut from the default
/// branch's head, a pull request to the default branch, and, where no person approves, the merge in the project's merge
/// mode. A merge the host arms or refuses is said, not failed: the pull request stands for a person either way.
/// <para>
/// 2026-11-23-c81ea: or, in COMMIT mode, one commit straight onto the default branch — no pull request, no merge, no
/// CI read — re-planned from the new head when another commit landed first.
/// </para>
/// </summary>
public sealed class GitOpsDelivery(ISourceProviderFactory sources, DeployCiStatus ci)
{
    /// <summary>Whether the pull request stands, the sentence that says where it stands, and its url.</summary>
    public async Task<(bool Opened, string Sentence, string? Url)> DeliverAsync(GitOpsOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var provider = sources.Create(order.Repo);
        var write = await provider.WriteFilesToBranchAsync(
            order.Branch, [new RepoFile(order.Path, order.Content)], order.Title, cancellationToken);
        if (!write.Written) return (false, $"the manifest could not be written to `{order.Branch.Value}`: {write.Error}", null);
        var url = await provider.CreatePullRequestAsync(
            new Repository(order.Branch, order.Repo.Url), order.Title, order.Body, cancellationToken);
        var stands = order.Approval
            ? $"pull request {url} waits for a person to merge it"
            : Merged(url, await provider.MergePullRequestAsync(url, order.Branch, order.Protection, cancellationToken));
        return (true, $"{stands}; {await ci.ReadAsync(provider, url, order.Ci, cancellationToken)}", url);
    }

    /// <summary>
    /// Writes <paramref name="order"/>'s files onto the default branch in one commit. On a moved head asks
    /// <paramref name="replan"/> for the order computed from the new head (null: the branch already holds it) and writes
    /// again, up to <paramref name="attempts"/> writes in all (pocs.gitops.commit_attempts).
    /// </summary>
    public async Task<(bool Committed, string? Sha, string Sentence)> CommitAsync(
        GitOpsCommitOrder order, Func<CancellationToken, Task<GitOpsCommitOrder?>> replan, int attempts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(replan);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        var provider = sources.Create(order.Repo);
        var branch = await provider.GetDefaultBranchAsync(cancellationToken);
        for (var attempt = 1; ; attempt++)
        {
            var write = await provider.WriteFilesToBranchAsync(branch, order.Files, order.Message, cancellationToken);
            if (!write.Conflict) return Committed(write, branch);
            if (attempt >= attempts)
                return (false, null, $"gave up after {attempts} attempts (pocs.gitops.commit_attempts): "
                    + $"other commits kept landing on `{branch.Value}`");
            if (await replan(cancellationToken) is not { } next) return (true, null, $"`{branch.Value}` already holds it");
            order = next;
        }
    }

    private static (bool, string?, string) Committed(BranchWriteResult write, BranchName branch) =>
        write.Written ? (true, write.CommitSha, $"committed {write.CommitSha} on `{branch.Value}`")
        : write.AlreadyHeld ? (true, null, $"`{branch.Value}` already holds it")
        : (false, null, $"the commit could not be written to `{branch.Value}`: {write.Error}");

    private static string Merged(string url, PullRequestCompletion merge) => merge.Outcome switch
    {
        PullRequestCompletionOutcome.Merged => $"pull request {url} merged",
        PullRequestCompletionOutcome.Armed => $"pull request {url} merges once the host's policy is met ({merge.Reason})",
        _ => $"pull request {url} is open: the host refused the merge ({merge.Reason}), so it waits for a person",
    };
}
