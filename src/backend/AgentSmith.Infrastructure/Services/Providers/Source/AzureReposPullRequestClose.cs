using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-15-114fa: closes an Azure Repos pull request without completing it — the update call with
/// <c>Status = Abandoned</c>, which the review read answers as closed. The id parser is
/// <see cref="AzureReposPullRequestUpdater"/>'s. A host failure is thrown.
/// </summary>
public sealed class AzureReposPullRequestClose(
    string project, string repoName, IAzDoClientFactory clientFactory, string organizationUrl, string personalAccessToken)
{
    public async Task CloseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId))
            throw new ArgumentException($"'{prUrl}' is not an Azure Repos pull request URL.", nameof(prUrl));
        await clientFactory.CreateGitClient(organizationUrl, personalAccessToken).UpdatePullRequestAsync(
            new GitPullRequest { Status = PullRequestStatus.Abandoned }, project, repoName, prId,
            cancellationToken: cancellationToken);
    }
}
