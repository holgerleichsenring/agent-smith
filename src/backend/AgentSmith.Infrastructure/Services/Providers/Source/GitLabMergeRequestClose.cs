using System.Net.Http.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-15-114fa: closes a GitLab merge request without merging it — <c>PUT merge_requests/{iid}</c> with
/// <c>state_event: close</c>, the PRIVATE-TOKEN header as every merge-request write. The iid parser is
/// <see cref="GitLabMergeRequestUpdater"/>'s. A refusing host is thrown, never reported as text.
/// </summary>
public sealed class GitLabMergeRequestClose(string baseUrl, string projectPath, string privateToken, HttpClient httpClient)
{
    public async Task CloseAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"'{prUrl}' is not a GitLab merge request URL.", nameof(prUrl));
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}")
        {
            Content = JsonContent.Create(new { state_event = "close" }),
        };
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
