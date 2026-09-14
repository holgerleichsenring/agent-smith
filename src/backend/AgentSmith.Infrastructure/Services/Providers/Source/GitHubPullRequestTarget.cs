using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-13-a284: reads and changes the BASE of an already-open GitHub pull request —
/// <c>PATCH /pulls/{n}</c> with a new <c>base</c>.
/// <para>
/// Its own type rather than a third pair of methods on
/// <see cref="GitHubPullRequestUpdater"/>: that type is at the line limit the coding
/// principles enforce, and where a pull request POINTS is a different question from what
/// it says and whether it is merged. The pull-number parser is shared with it, so one URL
/// shape is understood in one place.
/// </para>
/// </summary>
public sealed class GitHubPullRequestTarget(
    string owner, string repo, IGitHubClientFactory clientFactory, string token, ILogger logger)
{
    public async Task<string?> ReadBaseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var prNumber)) return null;
        _ = cancellationToken;
        try
        {
            return (await Client().PullRequest.Get(owner, repo, prNumber)).Base?.Ref;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the base branch of PR #{Pr}", prNumber);
            return null;
        }
    }

    public async Task<bool> MoveAsync(string prUrl, string target, CancellationToken cancellationToken)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var prNumber)) return false;
        _ = cancellationToken;
        try
        {
            await Client().PullRequest.Update(
                owner, repo, prNumber, new PullRequestUpdate { Base = target });
            logger.LogInformation("PR #{Pr} now targets {Target}", prNumber, target);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not move PR #{Pr} onto {Target}", prNumber, target);
            return false;
        }
    }

    private IGitHubClient Client() => clientFactory.Create(token);
}
