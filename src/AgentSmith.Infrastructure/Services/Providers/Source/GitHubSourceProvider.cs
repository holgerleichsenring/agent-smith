using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitHub repositories. CheckoutAsync is metadata-only —
/// the actual git clone happens sandbox-side via Step{Kind=Run, Command=git, ...}.
/// Default-branch resolution stays here (it is metadata, not git plumbing).
/// </summary>
public sealed class GitHubSourceProvider : ISourceProvider, IPrCommentProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _cloneUrl;
    private readonly string _token;
    private readonly string? _configuredDefaultBranch;
    private readonly ILogger<GitHubSourceProvider> _logger;
    private string? _cachedDefaultBranch;

    public string ProviderType => "GitHub";

    public GitHubSourceProvider(
        string repoUrl, string token, ILogger<GitHubSourceProvider> logger,
        string? defaultBranch = null)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _cloneUrl = repoUrl.EndsWith(".git") ? repoUrl : $"{repoUrl}.git";
        _token = token;
        _configuredDefaultBranch = defaultBranch;
        _logger = logger;
    }

    public async Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(CreateGitHubClient()));
        _logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken)
    {
        var client = CreateGitHubClient();
        var targetBranch = await GetDefaultBranchAsync(client);
        var pr = await client.PullRequest.Create(
            _owner, _repo,
            new NewPullRequest(title, repository.CurrentBranch.Value, targetBranch)
            {
                Body = description
            });

        _logger.LogInformation("Pull request created: {Url}", pr.HtmlUrl);
        return pr.HtmlUrl;
    }

    private async Task<string> GetDefaultBranchAsync(GitHubClient client)
    {
        if (_configuredDefaultBranch is not null)
            return _configuredDefaultBranch;

        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var repo = await client.Repository.Get(_owner, _repo);
            _cachedDefaultBranch = repo.DefaultBranch;
            _logger.LogDebug("Resolved default branch from GitHub API: {Branch}", _cachedDefaultBranch);
            return _cachedDefaultBranch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve default branch from GitHub API, falling back to 'main'");
            _cachedDefaultBranch = "main";
            return _cachedDefaultBranch;
        }
    }

    public async Task PostCommentAsync(
        string prIdentifier, string markdown, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        var client = CreateGitHubClient();
        await client.Issue.Comment.Create(_owner, _repo, prNumber, markdown);
        _logger.LogInformation("Posted comment on PR #{PrNumber}", prNumber);
    }

    private GitHubClient CreateGitHubClient()
    {
        var client = new GitHubClient(new ProductHeaderValue("AgentSmith"));
        client.Credentials = new Octokit.Credentials(_token);
        return client;
    }

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var uri = new Uri(url.Replace(".git", ""));
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new ConfigurationException($"Invalid GitHub URL: {url}");

        return (segments[0], segments[1]);
    }
}
