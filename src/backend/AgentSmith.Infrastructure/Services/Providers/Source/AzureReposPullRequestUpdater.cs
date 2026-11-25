using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// p0490: changes the state of an already-opened Azure Repos pull request, addressed
/// by the web URL <c>CreatePullRequestAsync</c> handed back — its description, its draft
/// flag, or whether it is completed. Opening one needs a repository, a branch and a
/// default-branch lookup; changing one that exists needs only the id recovered from that
/// URL, which is why the three edits share this type and its single parser.
/// </summary>
public sealed class AzureReposPullRequestUpdater(
    string project, string repoName, IAzDoClientFactory clientFactory,
    string organizationUrl, string personalAccessToken, ILogger logger)
{
    private readonly AzureReposAutoComplete _autoComplete = new(project, repoName, logger);
    private readonly AzureReposPolicyBypass _bypass = new(project, repoName, logger); // 2026-11-17-023fa

    public async Task<bool> UpdateBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            await UpdateAsync(
                new GitPullRequest
                {
                    Description = AzureReposSourceProvider.TruncateDescription(newBody),
                },
                prId, cancellationToken);
            logger.LogInformation("Updated PR body for !{PrId}", prId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to update PR body for !{PrId} ({Length} chars, limit {Limit})",
                prId, newBody?.Length ?? 0, AzureReposSourceProvider.MaxDescriptionChars);
            return false;
        }
    }

    // p0393a: Azure DevOps takes a pull request out of draft with the same update call
    // that sets the description.
    public async Task<bool> MarkReadyAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            await UpdateAsync(new GitPullRequest { IsDraft = false }, prId, cancellationToken);
            logger.LogInformation("PR !{PrId} is out of draft — the sequence completed", prId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark PR !{PrId} ready for review", prId);
            return false;
        }
    }

    // p0501: an immediate merge (Status = Completed) is the one thing a branch policy
    // refuses, so the pull request is ARMED instead and finishes itself when
    // the required build goes green. See AzureReposAutoComplete for why that identity.
    public Task<PullRequestCompletion> CompleteAsync(string prUrl, CancellationToken cancellationToken) =>
        CompleteAsync(prUrl, BranchProtection.Respect, MergeStyle.HostDefault, cancellationToken);

    // 2026-11-17-023fa: Bypass completes now past the policies; Respect arms as init does (2026-11-25-9992h: no vote).
    // 2026-11-25-9992i: a squash style completes with mergeStrategy squash either way.
    public Task<PullRequestCompletion> CompleteAsync(string prUrl, BranchProtection protection, MergeStyle style, CancellationToken cancellationToken) =>
        OnPullRequestAsync(prUrl, (client, pr, prId) => protection == BranchProtection.Bypass
            ? _bypass.CompleteAsync(client, pr, prId, style, cancellationToken)
            : ArmAsync(client, pr, prId, style, cancellationToken), cancellationToken);

    // 2026-11-25-9992h: armed as the token's own identity (the machine), read from the connection.
    private async Task<PullRequestCompletion> ArmAsync(GitHttpClient client, GitPullRequest pr, int prId, MergeStyle style, CancellationToken ct) =>
        await _autoComplete.ArmAsync(client, pr, prId, await SelfAsync(ct), style, ct);

    private Task<string?> SelfAsync(CancellationToken ct) => clientFactory.AuthorizedIdentityIdAsync(organizationUrl, personalAccessToken, ct);

    // 2026-11-24-629ab: auto-complete armed again after a conflict dropped it — no vote (AzureReposAutoComplete.RearmAsync).
    public Task<PullRequestCompletion> RearmAsync(string prUrl, CancellationToken cancellationToken) =>
        OnPullRequestAsync(prUrl, async (client, pr, prId) =>
            await _autoComplete.RearmAsync(client, pr, prId, await SelfAsync(cancellationToken), cancellationToken), cancellationToken);

    private async Task<PullRequestCompletion> OnPullRequestAsync(
        string prUrl, Func<GitHttpClient, GitPullRequest, int, Task<PullRequestCompletion>> act, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId))
            return PullRequestCompletion.Refused($"'{prUrl}' is not an Azure Repos pull request URL.");
        try
        {
            var client = CreateGitClient();
            return await act(client, await client.GetPullRequestAsync(project, repoName, prId, cancellationToken: cancellationToken), prId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Completing PR !{PrId} was refused", prId);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    private Task<GitPullRequest> UpdateAsync(
        GitPullRequest update, int prId, CancellationToken cancellationToken) =>
        CreateGitClient().UpdatePullRequestAsync(
            update, project, repoName, prId, cancellationToken: cancellationToken);

    private GitHttpClient CreateGitClient() =>
        clientFactory.CreateGitClient(organizationUrl, personalAccessToken);

    // 2026-09-13-a284: internal so the retarget type reads the SAME url shape, once.
    internal static bool TryParsePullRequestId(string prUrl, out int prId)
    {
        prId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pullrequest/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prId);
    }
}
