using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Octokit;
using Repository = AgentSmith.Domain.Entities.Repository;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Providers.Source;

/// <summary>
/// Source provider for GitHub repositories. Uses LibGit2Sharp for git ops, Octokit for PRs.
/// </summary>
public sealed class GitHubSourceProvider : ISourceProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _cloneUrl;
    private readonly string _token;
    private readonly ILogger<GitHubSourceProvider> _logger;

    public string ProviderType => "GitHub";

    public GitHubSourceProvider(
        string repoUrl, string token, ILogger<GitHubSourceProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _cloneUrl = repoUrl.EndsWith(".git") ? repoUrl : $"{repoUrl}.git";
        _token = token;
        _logger = logger;
    }

    public Task<Repository> CheckoutAsync(
        BranchName branch, CancellationToken cancellationToken = default)
    {
        var localPath = GetLocalPath();
        EnsureCloned(localPath);

        using var repo = new LibGit2Sharp.Repository(localPath);
        var existingBranch = repo.Branches[branch.Value];
        var targetBranch = existingBranch ?? repo.CreateBranch(branch.Value);
        Commands.Checkout(repo, targetBranch);

        _logger.LogInformation(
            "Checked out branch {Branch} in {Path}", branch, localPath);

        return Task.FromResult(new Repository(localPath, branch, _cloneUrl));
    }

    public Task CommitAndPushAsync(
        Repository repository, string message, CancellationToken cancellationToken = default)
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
        CancellationToken cancellationToken = default)
    {
        var client = CreateGitHubClient();
        var pr = await client.PullRequest.Create(
            _owner, _repo,
            new NewPullRequest(title, repository.CurrentBranch.Value, "main")
            {
                Body = description
            });

        _logger.LogInformation("Pull request created: {Url}", pr.HtmlUrl);
        return pr.HtmlUrl;
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
