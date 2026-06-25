using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: lists the repositories of a GitHub owner (org or user) via the REST API, paged at
/// 100/req. Bearer token from GITHUB_TOKEN (SourceProviderFactory convention). Falls back from
/// the org endpoint to the user endpoint when the owner is a user account.
/// </summary>
public sealed class GitHubRepoDiscoveryProvider(SecretsProvider secrets, ILogger<GitHubRepoDiscoveryProvider> logger)
    : IRepoDiscoveryProvider
{
    private const int PageSize = 100;
    private static readonly HttpClient Http = new();

    public RepoType Type => RepoType.GitHub;

    public async Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connection.Owner))
            throw new ConfigurationException($"Connection '{connection.Name}' (github) requires 'owner' for discovery.");

        var apiHost = string.IsNullOrEmpty(connection.Host) ? "https://api.github.com" : connection.Host.TrimEnd('/');
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var all = new List<DiscoveredRepo>();

        for (var page = 1; ; page++)
        {
            var url = $"{apiHost}/orgs/{connection.Owner}/repos?per_page={PageSize}&page={page}";
            var body = await GetPageAsync(url, token, connection, page == 1, cancellationToken);
            var batch = Parse(body);
            all.AddRange(batch);
            if (batch.Count < PageSize) break;
        }
        return all;
    }

    private async Task<string> GetPageAsync(
        string url, string token, ResolvedConnection connection, bool firstPage, CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, token, cancellationToken);

        // An owner that is a user (not an org) → /orgs 404s; retry against /users on the first page.
        if (firstPage && response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            response = await SendAsync(url.Replace("/orgs/", "/users/"), token, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GitHub repo discovery for '{connection.Name}' failed: HTTP {(int)response.StatusCode}.");

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("agent-smith");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return await Http.SendAsync(request, cancellationToken);
    }

    private static IReadOnlyList<DiscoveredRepo> Parse(string body)
    {
        var repos = JsonSerializer.Deserialize<List<GitHubRepo>>(body);
        if (repos is null) return Array.Empty<DiscoveredRepo>();
        return repos
            .Where(r => !string.IsNullOrEmpty(r.Name))
            .Select(r => new DiscoveredRepo { Name = r.Name!, Url = r.CloneUrl ?? string.Empty, DefaultBranch = r.DefaultBranch })
            .ToList();
    }

    private sealed class GitHubRepo
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("clone_url")] public string? CloneUrl { get; set; }
        [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
    }
}
