using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Fetches PR diff from GitHub via Octokit.
/// GET /repos/{owner}/{repo}/pulls/{number}/files
/// </summary>
public sealed class GitHubPrDiffProvider(
    IGitHubClient gitHubClient,
    string owner,
    string repoName,
    ILogger<GitHubPrDiffProvider> logger) : IPrDiffProvider
{
    public async Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        logger.LogInformation("Fetching PR #{PrNumber} diff from GitHub {Owner}/{Repo}", prNumber, owner, repoName);

        var pr = await gitHubClient.PullRequest.Get(owner, repoName, prNumber);
        var files = await gitHubClient.PullRequest.Files(owner, repoName, prNumber);

        var changedFiles = files.Select(f => new ChangedFile(
            f.FileName,
            f.Patch ?? string.Empty,
            MapStatus(f.Status))).ToList();

        return new PrDiff(pr.Base.Sha, pr.Head.Sha, changedFiles);
    }

    private static ChangeKind MapStatus(string status) => status switch
    {
        "added" => ChangeKind.Added,
        "removed" => ChangeKind.Deleted,
        _ => ChangeKind.Modified
    };
}
