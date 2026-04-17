using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Octokit;
using Repository = AgentSmith.Domain.Entities.Repository;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitHub repositories. Uses LibGit2Sharp for git ops, Octokit for PRs.
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

    public Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        var localPath = GetLocalPath();
        EnsureCloned(localPath);

        var target = branch ?? new BranchName("main");

        using var repo = new LibGit2Sharp.Repository(localPath);
        var existingBranch = repo.Branches[target.Value];
        var targetBranch = existingBranch ?? repo.CreateBranch(target.Value);
        Commands.Checkout(repo, targetBranch);

        _logger.LogInformation(
            "Checked out branch {Branch} in {Path}", target, localPath);

        return Task.FromResult(new Repository(localPath, target, _cloneUrl));
    }

    public Task CommitAndPushAsync(
        Repository repository, string message, CancellationToken cancellationToken)
    {
        using var repo = new LibGit2Sharp.Repository(repository.LocalPath);

        StageAllChanges(repo);
        CommitChanges(repo, message);
        PushToRemote(repo);

        _logger.LogInformation("Committed and pushed changes: {Message}", message);
        return Task.CompletedTask;
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

    private string GetLocalPath()
    {
        return Path.Combine(
            Path.GetTempPath(), "agentsmith", _owner, _repo);
    }

    private void EnsureCloned(string localPath)
    {
        if (LibGit2Sharp.Repository.IsValid(localPath))
        {
            _logger.LogDebug("Repository already cloned at {Path}", localPath);
            return;
        }

        if (Directory.Exists(localPath))
        {
            _logger.LogWarning(
                "Path {Path} exists but is not a valid repository, removing it", localPath);
            Directory.Delete(localPath, recursive: true);
        }

        _logger.LogInformation("Cloning {Url} to {Path}", _cloneUrl, localPath);
        var options = new CloneOptions
        {
            FetchOptions = { CredentialsProvider = GetCredentialsHandler() }
        };
        LibGit2Sharp.Repository.Clone(_cloneUrl, localPath, options);
    }

    private void StageAllChanges(LibGit2Sharp.Repository repo)
    {
        Commands.Stage(repo, "*");
    }

    private void CommitChanges(LibGit2Sharp.Repository repo, string message)
    {
        var signature = GetSignature(repo);
        repo.Commit(message, signature, signature);
    }

    private void PushToRemote(LibGit2Sharp.Repository repo)
    {
        var remote = repo.Network.Remotes["origin"]
            ?? throw new ProviderException(ProviderType, "No 'origin' remote configured.");

        var options = new PushOptions
        {
            CredentialsProvider = GetCredentialsHandler()
        };

        var canonicalName = repo.Head.CanonicalName;
        // Force push (+) so re-runs on the same ticket don't fail with
        // "non-fastforwardable reference" when the branch already exists on the remote.
        var refspec = $"+{canonicalName}:{canonicalName}";
        repo.Network.Push(remote, refspec, options);
    }

    private CredentialsHandler GetCredentialsHandler()
    {
        return (_, _, _) =>
            new UsernamePasswordCredentials { Username = _token, Password = string.Empty };
    }

    private static Signature GetSignature(LibGit2Sharp.Repository repo)
    {
        var config = repo.Config;
        var name = config.GetValueOrDefault("user.name", "Agent Smith");
        var email = config.GetValueOrDefault("user.email", "agent-smith@noreply.local");
        return new Signature(name, email, DateTimeOffset.Now);
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
