using System.Net;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: the questions GitLab's branch write asks before it posts — whether the branch exists and whether
/// a path is on it. The create-or-update probe reads any non-success as absent, as it always did: a wrong guess is
/// refused by the host. The deletion probe reads ONLY a 404 as absent and throws otherwise, so a transient error
/// never drops a required deletion.
/// </summary>
public sealed class GitLabFileProbe(string baseUrl, string projectPath, string privateToken, HttpClient httpClient)
{
    public async Task<bool> BranchExistsAsync(string branch, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, $"repository/branches/{Uri.EscapeDataString(branch)}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> FileExistsLenientAsync(string branch, string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Head, FileUrl(branch, path), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> FileExistsStrictAsync(string branch, string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Head, FileUrl(branch, path), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private static string FileUrl(string branch, string path) =>
        $"repository/files/{Uri.EscapeDataString(path)}?ref={Uri.EscapeDataString(branch)}";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relative, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{baseUrl}/api/v4/projects/{projectPath}/{relative}");
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        return await httpClient.SendAsync(request, ct);
    }
}
