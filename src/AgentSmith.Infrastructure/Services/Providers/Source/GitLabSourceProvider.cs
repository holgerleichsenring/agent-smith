using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitLab repositories. CheckoutAsync is metadata-only —
/// the actual git clone happens sandbox-side via Step{Kind=Run, Command=git, ...}.
/// Default-branch resolution stays here (REST API call, not git plumbing).
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

    public async Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(cancellationToken));
        _logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
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
}
