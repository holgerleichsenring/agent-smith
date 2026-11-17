using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-023fa: a code run's merge of a GitHub pull request. Bypass merges now through the REST merge init uses,
/// as the token's identity — an admin passes branch protection unless admins are enforced, a ruleset's bypass actor
/// passes its rules; a refusal names that permission and the key that waits instead. Respect enables GitHub's
/// auto-merge, so the protection decides when; a pull request with nothing left to wait for ("clean status") merges now.
/// </summary>
public sealed class GitHubMergeModes(
    GitHubRepoAddress address, IGitHubClientFactory clientFactory, string token,
    GitHubPullRequestMerger merger, ILogger logger)
{
    public const string Policy = "branch protection";
    public const string Permission =
        "the token admin rights on the repository with 'Do not allow bypassing the above settings' off, or its actor on the ruleset's bypass list";

    private const string Lookup =
        "query($owner: String!, $name: String!, $number: Int!) { repository(owner: $owner, name: $name) { mergeCommitAllowed squashMergeAllowed pullRequest(number: $number) { id } } }";

    private const string Mutation =
        "mutation($id: ID!, $method: PullRequestMergeMethod!) { enablePullRequestAutoMerge(input: {pullRequestId: $id, mergeMethod: $method}) { clientMutationId } }";

    public async Task<PullRequestCompletion> MergeAsync(
        string prUrl, string sourceBranch, BranchProtection protection, CancellationToken ct)
    {
        if (protection == BranchProtection.Respect) return await ArmAsync(prUrl, sourceBranch, ct);
        var completion = await merger.CompleteAsync(prUrl, sourceBranch, ct);
        return completion.Outcome == PullRequestCompletionOutcome.Refused
            ? BypassRefusal.Of(Policy, "GitHub", completion.Reason ?? "GitHub refused the merge", Permission)
            : completion;
    }

    private async Task<PullRequestCompletion> ArmAsync(string prUrl, string sourceBranch, CancellationToken ct)
    {
        if (!GitHubPullRequestUpdater.TryParsePullNumber(prUrl, out var number))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitHub pull request URL.");
        try
        {
            var query = new GitHubGraphQlQuery(clientFactory.Create(address, token), address.Api.GraphQl);
            var found = (await query.RunAsync(Lookup,
                new { owner = address.Owner, name = address.Repo, number }, "pull request lookup")).GetProperty("repository");
            await query.RunAsync(Mutation, new
            {
                id = found.GetProperty("pullRequest").GetProperty("id").GetString(), method = MethodOf(found),
            }, "enablePullRequestAutoMerge");
            logger.LogInformation("PR #{Pr}: auto-merge enabled", number);
            return PullRequestCompletion.Armed("GitHub auto-merge is enabled; it merges when the branch protection passes.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("clean status", StringComparison.OrdinalIgnoreCase))
        {
            // Nothing is left to wait for, so there is nothing to respect: merge it now.
            return await merger.CompleteAsync(prUrl, sourceBranch, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Enabling auto-merge on PR #{Pr} was refused", number);
            return PullRequestCompletion.Refused(ex.Message.Contains("auto merge", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("auto-merge", StringComparison.OrdinalIgnoreCase)
                ? $"GitHub auto-merge is off for this repository (allow_auto_merge); turn it on, or merge #{number} by hand: {ex.Message}"
                : ex.Message);
        }
    }

    /// <summary>The first merge method the repository allows, in GraphQL's spelling.</summary>
    public static string MethodOf(System.Text.Json.JsonElement repository) =>
        Allowed(repository, "mergeCommitAllowed") ? "MERGE" : Allowed(repository, "squashMergeAllowed") ? "SQUASH" : "REBASE";

    private static bool Allowed(System.Text.Json.JsonElement repository, string name) =>
        !repository.TryGetProperty(name, out var value) || value.ValueKind != System.Text.Json.JsonValueKind.False;
}
