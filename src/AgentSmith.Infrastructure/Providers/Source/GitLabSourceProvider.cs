using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Repository = AgentSmith.Domain.Entities.Repository;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Providers.Source;

/// <summary>
/// Source provider for GitLab repositories. Uses LibGit2Sharp for git ops, REST API v4 for merge requests.
/// </summary>
public sealed class GitLabSourceProvider : ISourceProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _cloneUrl;
    private readonly string _privateToken;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabSourceProvider> _logger;

    public string ProviderType => "GitLab";

    public GitLabSourceProvider(
        string baseUrl,
        string projectPath,
        string cloneUrl,
        string privateToken,
        HttpClient httpClient,
        ILogger<GitLabSourceProvider> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _projectPath = projectPath;
        _cloneUrl = cloneUrl;
        _privateToken = privateToken;
        _httpClient = httpClient;
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
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new
        {
            source_branch = repository.CurrentBranch.Value,
            target_branch = "main",
            title,
            description
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var webUrl = json.RootElement.GetProperty("web_url").GetString()
            ?? throw new ProviderException(ProviderType, "Merge request response did not contain a web_url.");

        _logger.LogInformation("Merge request created: {Url}", webUrl);
        return webUrl;
    }

    private string GetLocalPath()
    {
        return Path.Combine(
            Path.GetTempPath(), "agentsmith", "gitlab", _projectPath);
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
        var refspec = $"{canonicalName}:{canonicalName}";
        repo.Network.Push(remote, refspec, options);
    }

    private CredentialsHandler GetCredentialsHandler()
    {
        return (_, _, _) =>
            new UsernamePasswordCredentials { Username = "oauth2", Password = _privateToken };
    }

    private static Signature GetSignature(LibGit2Sharp.Repository repo)
    {
        var config = repo.Config;
        var name = config.GetValueOrDefault("user.name", "Agent Smith");
        var email = config.GetValueOrDefault("user.email", "agent-smith@noreply.local");
        return new Signature(name, email, DateTimeOffset.Now);
    }
}
