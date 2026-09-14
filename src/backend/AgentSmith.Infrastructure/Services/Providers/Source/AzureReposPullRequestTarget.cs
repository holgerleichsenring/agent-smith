using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-09-13-a284: reads and changes the <c>TargetRefName</c> of an already-open Azure
/// Repos pull request.
/// <para>
/// Its own type rather than a third pair of methods on
/// <see cref="AzureReposPullRequestUpdater"/>: where a pull request POINTS is a different
/// question from what it says and whether it is completed, and that type is close to the
/// line limit the coding principles enforce. The id parser is shared with it, so one URL
/// shape is understood in one place.
/// </para>
/// </summary>
public sealed class AzureReposPullRequestTarget(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    private const string HeadPrefix = "refs/heads/";

    public async Task<string?> ReadBaseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId)) return null;
        try
        {
            var pr = await CreateGitClient().GetPullRequestAsync(
                project, repoName, prId, cancellationToken: cancellationToken);
            // The caller compares against a short branch name — the ref is the wire form.
            return pr.TargetRefName?.StartsWith(HeadPrefix, StringComparison.Ordinal) == true
                ? pr.TargetRefName[HeadPrefix.Length..]
                : pr.TargetRefName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the target ref of PR !{PrId}", prId);
            return null;
        }
    }

    public async Task<bool> MoveAsync(string prUrl, string target, CancellationToken cancellationToken)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            await CreateGitClient().UpdatePullRequestAsync(
                new GitPullRequest { TargetRefName = $"{HeadPrefix}{target}" },
                project, repoName, prId, cancellationToken: cancellationToken);
            logger.LogInformation("PR !{PrId} now targets {Target}", prId, target);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not move PR !{PrId} onto {Target}", prId, target);
            return false;
        }
    }

    private GitHttpClient CreateGitClient() =>
        clientFactory.CreateGitClient(organizationUrl, personalAccessToken);
}
