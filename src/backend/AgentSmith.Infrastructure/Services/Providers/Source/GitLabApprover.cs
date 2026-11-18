using System.Text.Json;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435b: approves a GitLab merge request only when its approval rules still want approvals
/// (approvals_left above zero), as the token's user. GitLab's answer to a refused approval (an author forbidden to
/// approve, a user not eligible) is returned with its status and body.
/// </summary>
public sealed class GitLabApprover(string baseUrl, string projectPath, string token, HttpClient http)
{
    public async Task<PullRequestApproval> ApproveIfRequiredAsync(string prUrl, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            return PullRequestApproval.Refused($"Not a GitLab merge request URL: {prUrl}");
        var url = $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}";
        using (var read = await SendAsync(HttpMethod.Get, $"{url}/approvals", ct))
        {
            if (!read.IsSuccessStatusCode) return await RefusedAsync(read, ct);
            using var doc = await JsonDocument.ParseAsync(await read.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("approvals_left", out var left) || left.ValueKind != JsonValueKind.Number
                || left.GetInt32() <= 0) return PullRequestApproval.NotRequired;
        }
        using var approve = await SendAsync(HttpMethod.Post, $"{url}/approve", ct);
        return approve.IsSuccessStatusCode ? PullRequestApproval.Approved : await RefusedAsync(approve, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        return await http.SendAsync(request, ct);
    }

    private static async Task<PullRequestApproval> RefusedAsync(HttpResponseMessage response, CancellationToken ct) =>
        PullRequestApproval.Refused($"GitLab answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
}
