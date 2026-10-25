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
/// </summary>
public sealed class GitHubPullRequestMerger(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    private const string NoneAllowed =
        "The repository allows no merge method (allow_merge_commit, allow_squash_merge and allow_rebase_merge are all off).";

    public async Task<PullRequestCompletion> CompleteAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var prNumber))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitHub pull request URL.");
        _ = cancellationToken;
        try
        {
            var client = clientFactory.Create(address, token);
            var methods = Candidates(await client.Repository.Get(address.Owner, address.Repo));
            return methods.Count == 0 ? PullRequestCompletion.Refused(NoneAllowed) : await MergeAsync(client, prNumber, methods);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Merging PR #{Pr} was refused", prNumber);
            return PullRequestCompletion.Refused(ex.Message);
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

    private static IReadOnlyList<(PullRequestMergeMethod Method, bool Known)> Candidates(Octokit.Repository repository) =>
    [
        .. new (PullRequestMergeMethod Method, bool? Allowed)[]
        {
            (PullRequestMergeMethod.Merge, repository.AllowMergeCommit),
            (PullRequestMergeMethod.Squash, repository.AllowSquashMerge),
            (PullRequestMergeMethod.Rebase, repository.AllowRebaseMerge),
        }.Where(c => c.Allowed != false).Select(c => (c.Method, c.Allowed == true)),
    ];
}
