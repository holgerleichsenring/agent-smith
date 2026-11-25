using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbbc: completes a GitHub pull request with a merge method the repository allows —
/// merge commit, squash, rebase, the first its settings allow. A setting the response leaves null is
/// unknown: that method is tried, and a 405 moves on to the next one not known to be off. A 405 on a
/// method the repository is known to allow is the platform declining (p0490) and is reported as such.
/// 2026-11-17-713eb: a merged agent-smith/ head is deleted afterwards — GitHub has no per-pull-request
/// option for it. A delete that fails (a repository that deletes on merge itself answers 422) leaves the
/// completion Merged; the branch sweep removes what is left. 2026-11-25-9992i: a squash style merges by squash only — a
/// repository that turned squash off is refused by name rather than merged another way.
/// </summary>
public sealed class GitHubPullRequestMerger(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    private const string NoneAllowed =
        "The repository allows no merge method (allow_merge_commit, allow_squash_merge and allow_rebase_merge are all off).";

    private const string NoSquash = "The repository does not allow squash merges (allow_squash_merge), and a merge into a series branch squashes.";

    public async Task<PullRequestCompletion> CompleteAsync(
        string prUrl, string? sourceBranch, MergeStyle style, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var prNumber))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitHub pull request URL.");
        _ = cancellationToken;
        try
        {
            var client = clientFactory.Create(address, token);
            var methods = Candidates(await client.Repository.Get(address.Owner, address.Repo), style);
            if (methods.Count == 0) return PullRequestCompletion.Refused(style == MergeStyle.Squash ? NoSquash : NoneAllowed);
            var completion = await MergeAsync(client, prNumber, methods);
            if (completion.Outcome == PullRequestCompletionOutcome.Merged) await DeleteHeadAsync(client, sourceBranch);
            return completion;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Merging PR #{Pr} was refused", prNumber);
            return PullRequestCompletion.Refused(ex.Message) with { Conflicts = ex is PullRequestNotMergeableException && await DirtyAsync(prNumber) };
        }
    }

    private async Task<PullRequestCompletion> MergeAsync(
        IGitHubClient client, int prNumber, IReadOnlyList<(PullRequestMergeMethod Method, bool Known)> methods)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                var merge = await client.PullRequest.Merge(
                    address.Owner, address.Repo, prNumber, new MergePullRequest { MergeMethod = methods[i].Method });
                return Completed(merge, prNumber, methods[i].Method);
            }
            catch (PullRequestNotMergeableException ex) when (!methods[i].Known && i < methods.Count - 1)
            {
                logger.LogInformation("PR #{Pr}: {Method} merge refused ({Message}); trying {Next}",
                    prNumber, methods[i].Method, ex.Message, methods[i + 1].Method);
            }
        }
    }

    private async Task DeleteHeadAsync(IGitHubClient client, string? branch)
    {
        if (!WorkBranches.Owns(branch)) return;
        try
        {
            await client.Git.Reference.Delete(address.Owner, address.Repo, $"heads/{branch}");
            logger.LogInformation("Deleted merged branch {Branch}", branch);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Merged branch {Branch} was not deleted", branch);
        }
    }

    // 2026-11-24-f9c5e: GitHub refuses a merge that is not mergeable for several reasons; 'dirty' is the conflict.
    private async Task<bool> DirtyAsync(int prNumber)
    {
        try { return (await clientFactory.Create(address, token).PullRequest.Get(address.Owner, address.Repo, prNumber)).MergeableState?.StringValue == "dirty"; }
        catch (Exception) { return false; } // a best-effort read: an unreadable state is no conflict
    }

    private PullRequestCompletion Completed(PullRequestMerge merge, int prNumber, PullRequestMergeMethod method)
    {
        if (merge.Merged)
        {
            logger.LogInformation("PR #{Pr} merged ({Method})", prNumber, method);
            return PullRequestCompletion.Merged();
        }
        logger.LogInformation("PR #{Pr} was not merged: {Message}", prNumber, merge.Message);
        return PullRequestCompletion.Refused(merge.Message ?? "GitHub declined the merge.");
    }

    private static IReadOnlyList<(PullRequestMergeMethod Method, bool Known)> Candidates(Octokit.Repository repository, MergeStyle style) =>
        style == MergeStyle.Squash ? [.. All(repository).Where(c => c.Method == PullRequestMergeMethod.Squash)] : All(repository);

    private static IReadOnlyList<(PullRequestMergeMethod Method, bool Known)> All(Octokit.Repository repository) =>
    [
        .. new (PullRequestMergeMethod Method, bool? Allowed)[]
        {
            (PullRequestMergeMethod.Merge, repository.AllowMergeCommit),
            (PullRequestMergeMethod.Squash, repository.AllowSquashMerge),
            (PullRequestMergeMethod.Rebase, repository.AllowRebaseMerge),
        }.Where(c => c.Allowed != false).Select(c => (c.Method, c.Allowed == true)),
    ];
}
