using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-713ec: a GitLab project's agent-smith/ branches (searched with an anchored prefix, paged), the merge
/// requests from and into each, and the delete. GitLab's branch delete takes no expected commit; the run read the
/// branch's merge requests moments before.
/// </summary>
public sealed class GitLabWorkBranches(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient) : IWorkBranchCleanup
{
    private const int PageSize = 100;

    public async Task<IReadOnlyList<WorkBranchTip>> ListWorkBranchesAsync(CancellationToken cancellationToken)
    {
        var tips = new List<WorkBranchTip>();
        for (var page = 1; ; page++)
        {
            using var doc = await GetAsync(
                $"repository/branches?search=^{Uri.EscapeDataString(WorkBranches.Prefix + "/")}&per_page={PageSize}&page={page}",
                cancellationToken);
            var items = doc.RootElement.EnumerateArray().ToList();
            tips.AddRange(items.Select(b => new WorkBranchTip(
                b.GetProperty("name").GetString()!, b.GetProperty("commit").GetProperty("id").GetString()!)));
            if (items.Count < PageSize) break;
        }
        return [.. tips.Where(t => WorkBranches.Owns(t.Name))];
    }

    public async Task<BranchPullRequests> PullRequestsOfAsync(string branch, CancellationToken cancellationToken)
    {
        var name = Uri.EscapeDataString(branch);
        using var from = await GetAsync($"merge_requests?source_branch={name}&state=all&per_page={PageSize}", cancellationToken);
        using var into = await GetAsync($"merge_requests?target_branch={name}&state=opened&per_page={PageSize}", cancellationToken);
        return new BranchPullRequests(
            [.. from.RootElement.EnumerateArray().Select(Of)], into.RootElement.GetArrayLength());
    }

    public async Task<bool> DeleteBranchAsync(WorkBranchTip branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(branch);
        using var request = Request(HttpMethod.Delete, $"repository/branches/{Uri.EscapeDataString(branch.Name)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    private static BranchPullRequest Of(JsonElement mr) => new(
        mr.GetProperty("state").GetString() switch
        {
            "merged" => BranchPullRequestState.Merged,
            "closed" => BranchPullRequestState.Closed,
            _ => BranchPullRequestState.Open,
        },
        mr.TryGetProperty("sha", out var sha) ? sha.GetString() : null);

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{baseUrl}/api/v4/projects/{projectPath}/{path}");
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        return request;
    }
}
