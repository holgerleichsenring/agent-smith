using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Repository = AgentSmith.Domain.Entities.Repository;
using Signature = LibGit2Sharp.Signature;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitLab repositories. Uses LibGit2Sharp for git ops, REST API v4 for merge requests.
/// </summary>
public sealed class GitLabSourceProvider : ISourceProvider, IPrCommentProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _cloneUrl;
    private readonly string _privateToken;
    private readonly string? _configuredDefaultBranch;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabSourceProvider> _logger;
    private string? _cachedDefaultBranch;

    public string ProviderType => "GitLab";

    public GitLabSourceProvider(
        string baseUrl,
        string projectPath,
        string cloneUrl,
        string privateToken,
        HttpClient httpClient,
        ILogger<GitLabSourceProvider> logger,
        string? defaultBranch = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _projectPath = projectPath;
        _cloneUrl = cloneUrl;
        _privateToken = privateToken;
        _configuredDefaultBranch = defaultBranch;
        _httpClient = httpClient;
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
        var targetBranch = await GetDefaultBranchAsync(cancellationToken);
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/merge_requests";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new
        {
            source_branch = repository.CurrentBranch.Value,
            target_branch = targetBranch,
            title,
            description
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var webUrl = json.RootElement.GetProperty("web_url").GetString()
            ?? throw new ProviderException(ProviderType, "Merge request response did not contain a web_url.");

        _logger.LogInformation("Merge request created: {Url}", webUrl);
        return webUrl;
    }

    public async Task PostCommentAsync(
        string prIdentifier, string markdown, CancellationToken cancellationToken = default)
    {
        var encodedPath = Uri.EscapeDataString(_projectPath);
        var url = $"{_baseUrl}/api/v4/projects/{encodedPath}/merge_requests/{prIdentifier}/notes";
        var json = System.Text.Json.JsonSerializer.Serialize(new { body = markdown });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
        _logger.LogInformation("Posted comment on MR !{MrIid}", prIdentifier);
    }

    private async Task<string> GetDefaultBranchAsync(CancellationToken cancellationToken)
    {
        if (_configuredDefaultBranch is not null)
            return _configuredDefaultBranch;

        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var url = $"{_baseUrl}/api/v4/projects/{_projectPath}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("PRIVATE-TOKEN", _privateToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            _cachedDefaultBranch = json.RootElement.TryGetProperty("default_branch", out var branch)
                ? branch.GetString() ?? "main"
                : "main";

            _logger.LogDebug("Resolved default branch from GitLab API: {Branch}", _cachedDefaultBranch);
            return _cachedDefaultBranch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve default branch from GitLab API, falling back to 'main'");
            _cachedDefaultBranch = "main";
            return _cachedDefaultBranch;
        }
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
        // Force push (+) so re-runs on the same ticket don't fail with
        // "non-fastforwardable reference" when the branch already exists on the remote.
        var refspec = $"+{canonicalName}:{canonicalName}";
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
