using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-c81ea: every file under one folder of a branch — the tree call scoped to the folder with
/// <c>recursive=true</c>, page by page, blobs only; each entry's <c>path</c> is already repo-relative. Paging stops
/// once more files than the caller's bound are known, so an overfull folder is never read to its end.
/// </summary>
public sealed class GitLabFolderFiles(string baseUrl, string projectPath, string privateToken, HttpClient httpClient)
{
    private const string PageSize = "100"; // not a limit: GitLab's maximum page size, every page is read

    public async Task<RepoFileListing> ListAsync(string path, string branch, int maxFiles, CancellationToken ct)
    {
        var folder = path.Replace('\\', '/').Trim('/');
        var files = new List<string>();
        for (string? page = "1"; page is not null && files.Count <= maxFiles;)
        {
            using var response = await GetAsync(folder, branch, page, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return RepoFileListing.Empty;
            response.EnsureSuccessStatusCode();
            files.AddRange(await BlobsAsync(response, ct));
            page = response.Headers.TryGetValues("X-Next-Page", out var next)
                && next.FirstOrDefault() is { Length: > 0 } value ? value : null;
        }
        return RepoFileListing.Bounded(files, maxFiles, hostTruncated: false);
    }

    private static async Task<IReadOnlyList<string>> BlobsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var blobs = new List<string>();
        foreach (var item in json.RootElement.EnumerateArray())
            if (item.TryGetProperty("type", out var type) && type.GetString() == "blob"
                && item.TryGetProperty("path", out var path) && path.GetString() is { } p)
                blobs.Add(p);
        return blobs;
    }

    private async Task<HttpResponseMessage> GetAsync(string folder, string branch, string page, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/v4/projects/{projectPath}/repository/tree?path={Uri.EscapeDataString(folder)}"
            + $"&ref={Uri.EscapeDataString(branch)}&recursive=true&per_page={PageSize}&page={page}");
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        return await httpClient.SendAsync(request, ct);
    }
}
