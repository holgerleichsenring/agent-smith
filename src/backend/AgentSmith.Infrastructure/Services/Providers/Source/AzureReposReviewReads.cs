using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-e8b9d / 2026-10-08-e8b9c: a pull request's review threads and its standing "Wait for
/// author" votes, over the provider's Git client. The repository's ids come with the answer, because
/// an Azure DevOps author's trust verdict is keyed on them.
/// </summary>
public sealed class AzureReposReviewReads(
    Func<CancellationToken, Task<GitHttpClient>> connect, Func<CancellationToken, Task<string?>> self,
    string project, string repoName, string repoUrl) : IPrReviewThreadReader, IPrReviewActReader
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
        return AzureReposVoteActs.Standing(threads, pr.Reviewers ?? [], pr.CreatedBy?.Id, repoUrl,
            pr.Repository.Id.ToString(), pr.Repository.ProjectReference.Id.ToString());
    }

    private static int PrId(string prUrl) => AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId)
        ? prId : throw new ArgumentException($"Not an Azure Repos pull request URL: {prUrl}", nameof(prUrl));
}
