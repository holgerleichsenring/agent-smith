using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: lists the projects of a GitLab group via the REST API (incl. subgroups), paged at
/// 100/req. PRIVATE-TOKEN from GITLAB_TOKEN (SourceProviderFactory convention).
/// </summary>
public sealed class GitLabRepoDiscoveryProvider(SecretsProvider secrets, ILogger<GitLabRepoDiscoveryProvider> logger)
    : IRepoDiscoveryProvider
{
    private const int PageSize = 100;
    private static readonly HttpClient Http = new();

    public RepoType Type => RepoType.GitLab;

    public async Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connection.Group))
            throw new ConfigurationException($"Connection '{connection.Name}' (gitlab) requires 'group' for discovery.");

        var apiHost = string.IsNullOrEmpty(connection.Host) ? "https://gitlab.com" : connection.Host.TrimEnd('/');
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var group = Uri.EscapeDataString(connection.Group);
        var all = new List<DiscoveredRepo>();

        for (var page = 1; ; page++)
        {
            var url = $"{apiHost}/api/v4/groups/{group}/projects" +
                      $"?include_subgroups=true&per_page={PageSize}&page={page}";
            var batch = Parse(await GetPageAsync(url, token, connection, cancellationToken));
            all.AddRange(batch);
            if (batch.Count < PageSize) break;
        }
        return all;
    }

    private async Task<string> GetPageAsync(
        string url, string token, ResolvedConnection connection, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", token);

        var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GitLab repo discovery for '{connection.Name}' failed: HTTP {(int)response.StatusCode}.");

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static IReadOnlyList<DiscoveredRepo> Parse(string body)
    {
        var projects = JsonSerializer.Deserialize<List<GitLabProject>>(body);
        if (projects is null) return Array.Empty<DiscoveredRepo>();
        return projects
            .Where(p => !string.IsNullOrEmpty(p.Path))
            .Select(p => new DiscoveredRepo
            {
                Name = p.Path!,
                Url = p.HttpUrlToRepo ?? string.Empty,
                DefaultBranch = p.DefaultBranch,
            })
            .ToList();
    }

    private sealed class GitLabProject
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("http_url_to_repo")] public string? HttpUrlToRepo { get; set; }
        [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
    }
}
