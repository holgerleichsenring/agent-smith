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
            // 2026-08-26-5c85: with_shared=false — a project shared INTO the group has a
            // foreign namespace, so it can neither be named relative to the group nor
            // reached by a static ref (the URL builder prefixes the connection group).
            var url = $"{apiHost}/api/v4/groups/{group}/projects" +
                      $"?include_subgroups=true&with_shared=false&per_page={PageSize}&page={page}";
            var batch = Parse(await GetPageAsync(url, token, connection, cancellationToken), connection.Group);
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

    // 2026-08-26-5c85: internal — the test seam. The static HttpClient leaves
    // DiscoverAsync without an HTTP seam, so the API→DiscoveredRepo mapping is
    // pinned directly (same pattern as SourceProviderFactory.ResolveGitLabTarget).
    internal IReadOnlyList<DiscoveredRepo> Parse(string body, string group)
    {
        var projects = JsonSerializer.Deserialize<List<GitLabProject>>(body);
        if (projects is null) return Array.Empty<DiscoveredRepo>();
        return projects
            .Where(p => !string.IsNullOrEmpty(p.Path))
            .Select(p => new DiscoveredRepo
            {
                Name = NamespaceRelativeName(p, group),
                Url = p.HttpUrlToRepo ?? string.Empty,
                DefaultBranch = p.DefaultBranch,
            })
            .ToList();
    }

    // 2026-08-26-5c85: the bare slug is not unique across subgroups, so a repo is
    // named by its path RELATIVE to the connection's group ('team-a/api'); top-level
    // projects keep their short name. A project the group prefix cannot be stripped
    // from keeps the bare slug — degrading to the old (ambiguous) name beats failing
    // the whole connection, but never silently.
    private string NamespaceRelativeName(GitLabProject project, string group)
    {
        var prefix = group.Trim('/') + "/";
        var full = project.PathWithNamespace;
        if (!string.IsNullOrEmpty(full)
            && full.Length > prefix.Length
            && full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return full[prefix.Length..];

        logger.LogWarning(
            "GitLab discovery: project '{Project}' (path_with_namespace: '{Full}') does not live " +
            "under group '{Group}' — keeping its bare path as the repo name.",
            project.Path, full, group);
        return project.Path!;
    }

    private sealed class GitLabProject
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("path_with_namespace")] public string? PathWithNamespace { get; set; }
        [JsonPropertyName("http_url_to_repo")] public string? HttpUrlToRepo { get; set; }
        [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
    }
}
