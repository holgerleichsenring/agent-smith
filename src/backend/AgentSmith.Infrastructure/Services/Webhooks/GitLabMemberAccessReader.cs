using System.Net;
using System.Text.Json;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Asks the GitLab instance the repository lives on, with <c>GITLAB_TOKEN</c> (scope
/// <c>read_api</c>). The instance is the repository URL's own host unless <c>GITLAB_URL</c>
/// overrides it, the same rule the source provider follows.
/// </summary>
public sealed class GitLabMemberAccessReader(
    SecretsProvider secrets,
    IHttpClientFactory httpClientFactory) : IGitLabMemberAccessReader
{
    public async Task<int?> ReadAccessLevelAsync(
        string repositoryUrl, string projectId, string userId, CancellationToken cancellationToken)
    {
        var token = secrets.GetRequired("GITLAB_TOKEN");
        var (baseUrl, _, _) = SourceProviderFactory.ResolveGitLabTarget(
            repositoryUrl, secrets.GetOptional("GITLAB_URL"));
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
