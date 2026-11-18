using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: delivers a changed manifest the GitOps way — one commit on a fresh branch cut from the default
/// branch's head, a pull request to the default branch, and, where no person approves, the merge in the project's merge
/// mode. A merge the host arms or refuses is said, not failed: the pull request stands for a person either way.
/// </summary>
public sealed class GitOpsDelivery(ISourceProviderFactory sources, DeployCiStatus ci)
{
    /// <summary>Whether the pull request stands, and the sentence that says where it stands.</summary>
    public async Task<(bool Opened, string Sentence)> DeliverAsync(GitOpsOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var provider = sources.Create(order.Repo);
        var write = await provider.WriteFilesToBranchAsync(
            order.Branch, [new RepoFile(order.Path, order.Content)], order.Title, cancellationToken);
        if (!write.Written) return (false, $"the manifest could not be written to `{order.Branch.Value}`: {write.Error}");
        var url = await provider.CreatePullRequestAsync(
            new Repository(order.Branch, order.Repo.Url), order.Title, order.Body, cancellationToken);
        var stands = order.Approval
            ? $"pull request {url} waits for a person to merge it"
            : Merged(url, await provider.MergePullRequestAsync(url, order.Branch, order.Protection, cancellationToken));
        return (true, $"{stands}; {await ci.ReadAsync(provider, url, order.Ci, cancellationToken)}");
    }

    private static string Merged(string url, PullRequestCompletion merge) => merge.Outcome switch
    {
        PullRequestCompletionOutcome.Merged => $"pull request {url} merged",
        PullRequestCompletionOutcome.Armed => $"pull request {url} merges once the host's policy is met ({merge.Reason})",
        _ => $"pull request {url} is open: the host refused the merge ({merge.Reason}), so it waits for a person",
    };
}
