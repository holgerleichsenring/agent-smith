using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435b: an approving (+10) reviewer vote on an Azure Repos pull request, cast as one identity — the one
/// vote both auto-complete (p0501, as the pull request's creator) and a merge-green run's approval (as the identity
/// merge.approval_auth names, or the token's own) cast.
/// </summary>
public static class AzureReposApprovalVote
{
    private const short Approve = 10;

    public static Task CastAsync(
        GitHttpClient client, string project, string repoName, int prId, string reviewerId, CancellationToken ct) =>
        client.CreatePullRequestReviewerAsync(
            new IdentityRefWithVote { Vote = Approve }, project, repoName, prId, reviewerId, cancellationToken: ct);
}
