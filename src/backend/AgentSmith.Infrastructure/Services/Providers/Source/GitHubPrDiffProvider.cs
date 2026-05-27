using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Fetches PR diff from GitHub via Octokit.
/// GET /repos/{owner}/{repo}/pulls/{number}/files
/// </summary>
public sealed class GitHubPrDiffProvider : IPrDiffProvider
{
    private readonly IGitHubClient _gitHubClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger<GitHubPrDiffProvider> _logger;

    public GitHubPrDiffProvider(
        IGitHubClient gitHubClient,
        GitHubTicketConnection connection,
        ILogger<GitHubPrDiffProvider> logger)
    {
        _gitHubClient = gitHubClient;
        (_owner, _repo) = ParseGitHubUrl(connection.RepoUrl);
        _logger = logger;
    }

    public async Task<PrDiff> GetDiffAsync(string prIdentifier, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        _logger.LogInformation("Fetching PR #{PrNumber} diff from GitHub {Owner}/{Repo}", prNumber, _owner, _repo);

        var pr = await _gitHubClient.PullRequest.Get(_owner, _repo, prNumber);
        var files = await _gitHubClient.PullRequest.Files(_owner, _repo, prNumber);

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

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
