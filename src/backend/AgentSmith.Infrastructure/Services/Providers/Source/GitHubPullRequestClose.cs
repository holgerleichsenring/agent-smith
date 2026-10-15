using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-15-114fa: closes a GitHub pull request without merging it — <c>PATCH /pulls/{n}</c> with
/// <c>state: closed</c>. Its own type because <see cref="GitHubPullRequestUpdater"/> is at the line limit; the
/// pull-number parser is that type's, so one URL shape is understood in one place. A host failure is thrown.
/// </summary>
public sealed class GitHubPullRequestClose(string owner, string repo, IGitHubClient client)
{
    public async Task CloseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var prNumber))
            throw new ArgumentException($"'{prUrl}' is not a GitHub pull request URL.", nameof(prUrl));
        cancellationToken.ThrowIfCancellationRequested();
        await client.PullRequest.Update(owner, repo, prNumber, new PullRequestUpdate { State = ItemState.Closed });
    }
}
