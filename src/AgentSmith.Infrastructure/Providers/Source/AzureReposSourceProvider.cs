using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Repository = AgentSmith.Domain.Entities.Repository;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Providers.Source;

/// <summary>
/// Source provider for Azure DevOps repositories. Uses LibGit2Sharp for git ops,
/// Azure DevOps GitHttpClient for pull requests.
/// </summary>
public sealed class AzureReposSourceProvider : ISourceProvider
{
    private readonly string _organizationUrl;
    private readonly string _project;
    private readonly string _repoName;
    private readonly string _cloneUrl;
    private readonly string _personalAccessToken;
    private readonly ILogger<AzureReposSourceProvider> _logger;

    public string ProviderType => "AzureRepos";

    public AzureReposSourceProvider(
        string organizationUrl,
        string project,
        string repoName,
        string personalAccessToken,
        ILogger<AzureReposSourceProvider> logger)
    {
        _organizationUrl = organizationUrl.TrimEnd('/');
        _project = project;
        _repoName = repoName;
        _personalAccessToken = personalAccessToken;
        _logger = logger;
        _cloneUrl = $"{_organizationUrl}/{_project}/_git/{_repoName}";
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
        var client = CreateGitClient();
        var sourceBranch = $"refs/heads/{repository.CurrentBranch.Value}";
        const string targetBranch = "refs/heads/main";

        var pullRequest = new GitPullRequest
        {
            Title = title,
            Description = description,
            SourceRefName = sourceBranch,
            TargetRefName = targetBranch
        };

        try
        {
            var createdPr = await client.CreatePullRequestAsync(
                pullRequest, _project, _repoName, cancellationToken: cancellationToken);

            var prUrl = $"{_organizationUrl}/{_project}/_git/{_repoName}/pullrequest/{createdPr.PullRequestId}";
            _logger.LogInformation("Pull request created: {Url}", prUrl);
            return prUrl;
        }
        catch (Exception ex) when (ex.Message.Contains("TF401179") || ex.Message.Contains("already exists"))
        {
            // A PR for this branch already exists (e.g. from a previous re-run).
            // Find it and return its URL instead of failing.
            _logger.LogWarning("PR already exists for branch {Branch}, looking up existing PR...",
                repository.CurrentBranch.Value);

            var existingPrs = await client.GetPullRequestsAsync(
                _project,
                _repoName,
                new GitPullRequestSearchCriteria
                {
                    SourceRefName = sourceBranch,
                    TargetRefName = targetBranch,
                    Status = PullRequestStatus.Active
                },
                cancellationToken: cancellationToken);

            var existing = existingPrs.FirstOrDefault()
                ?? throw new ProviderException(ProviderType,
                    $"PR already exists for branch {repository.CurrentBranch.Value} but could not be found.");

            var existingUrl = $"{_organizationUrl}/{_project}/_git/{_repoName}/pullrequest/{existing.PullRequestId}";
            _logger.LogInformation("Found existing pull request: {Url}", existingUrl);
            return existingUrl;
        }
    }

    private string GetLocalPath()
    {
        return Path.Combine(
            Path.GetTempPath(), "agentsmith", "azuredevops", _project, _repoName);
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
            new UsernamePasswordCredentials { Username = string.Empty, Password = _personalAccessToken };
    }

    private static Signature GetSignature(LibGit2Sharp.Repository repo)
    {
        var config = repo.Config;
        var name = config.GetValueOrDefault("user.name", "Agent Smith");
        var email = config.GetValueOrDefault("user.email", "agent-smith@noreply.local");
        return new Signature(name, email, DateTimeOffset.Now);
    }

    private GitHttpClient CreateGitClient()
    {
        var credentials = new VssBasicCredential(string.Empty, _personalAccessToken);
        var connection = new VssConnection(new Uri(_organizationUrl), credentials);
        return connection.GetClient<GitHttpClient>();
    }
}
