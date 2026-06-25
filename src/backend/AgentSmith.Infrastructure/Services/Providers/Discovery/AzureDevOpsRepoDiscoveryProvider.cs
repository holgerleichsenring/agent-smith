using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Discovery;

/// <summary>
/// p0281a: lists the git repositories of an Azure DevOps project via
/// <c>{org}/{project}/_apis/git/repositories</c> (Basic auth, empty user + PAT). The PAT is
/// read from AZURE_DEVOPS_TOKEN, matching SourceProviderFactory's env convention.
/// </summary>
public sealed class AzureDevOpsRepoDiscoveryProvider(SecretsProvider secrets, ILogger<AzureDevOpsRepoDiscoveryProvider> logger)
    : IRepoDiscoveryProvider
{
    private static readonly HttpClient Http = new();

    public RepoType Type => RepoType.AzureDevOps;

    public async Task<IReadOnlyList<DiscoveredRepo>> DiscoverAsync(
        ResolvedConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connection.Organization) || string.IsNullOrEmpty(connection.Project))
            throw new ConfigurationException(
                $"Connection '{connection.Name}' (azure_devops) requires 'organization' and 'project' for discovery.");

        var host = string.IsNullOrEmpty(connection.Host) ? "https://dev.azure.com" : connection.Host.TrimEnd('/');
        var url = $"{host}/{connection.Organization}/{Uri.EscapeDataString(connection.Project)}" +
                  "/_apis/git/repositories?api-version=7.1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{secrets.GetRequired("AZURE_DEVOPS_TOKEN")}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

        var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Azure DevOps repo discovery for '{connection.Name}' failed: HTTP {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(body);
    }

    private static IReadOnlyList<DiscoveredRepo> Parse(string body)
    {
        var payload = JsonSerializer.Deserialize<AdoRepoList>(body);
        if (payload?.Value is null) return Array.Empty<DiscoveredRepo>();
        return payload.Value
            .Where(r => !string.IsNullOrEmpty(r.Name))
            .Select(r => new DiscoveredRepo
            {
                Name = r.Name!,
                Url = r.RemoteUrl ?? string.Empty,
                DefaultBranch = StripRefsHeads(r.DefaultBranch),
            })
            .ToList();
    }

    private static string? StripRefsHeads(string? branch) =>
        string.IsNullOrEmpty(branch) ? null
        : branch.StartsWith("refs/heads/", StringComparison.Ordinal) ? branch["refs/heads/".Length..]
        : branch;

    private sealed class AdoRepoList
    {
        [JsonPropertyName("value")] public List<AdoRepo>? Value { get; set; }
    }

    private sealed class AdoRepo
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; set; }
        [JsonPropertyName("defaultBranch")] public string? DefaultBranch { get; set; }
    }
}
