using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Asks the GitLab instance the repository lives on, with the repository's own auth secret
/// (scope <c>read_api</c>). The instance is the repository URL's own host unless the repo's
/// <c>Host</c> overrides it, the same rule the source provider follows (2026-10-02-5f89a).
/// </summary>
public sealed class GitLabMemberAccessReader(
    ICredentialResolver credentials,
    IHttpClientFactory httpClientFactory) : IGitLabMemberAccessReader
{
    public async Task<int?> ReadAccessLevelAsync(
        RepoConnection repo, string projectId, string userId, CancellationToken cancellationToken)
    {
        var token = credentials.For(repo);
        var (baseUrl, _, _) = SourceProviderFactory.ResolveGitLabTarget(repo.Url!, repo.Host);
        var url = $"{baseUrl}/api/v4/projects/{Uri.EscapeDataString(projectId)}"
                  + $"/members/all/{Uri.EscapeDataString(userId)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return doc.RootElement.GetProperty("access_level").GetInt32();
    }
}
