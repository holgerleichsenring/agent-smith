using System.Net;
using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: one file, one directory listing and branch existence on a NAMED ref, through
/// the same HttpClient and private token the provider uses. The provider's default-branch reads are
/// these with the resolved default as the ref.
/// <para>
/// <c>projectPath</c> is ALREADY url-escaped by the factory (namespace%2Fname), so it goes in raw —
/// re-escaping double-encodes it (%252F) and 404s nested-subgroup projects. Only the file path and
/// the ref are escaped here.
/// </para>
/// </summary>
public sealed class GitLabBranchRead(
    string baseUrl, string projectPath, string privateToken, HttpClient httpClient)
{
    /// <summary>False only on a 404; any other failure throws, so it is not read as "absent".</summary>
    public async Task<bool> ExistsAsync(string branch, CancellationToken ct)
    {
        using var response = await GetAsync(
            $"repository/branches/{Uri.EscapeDataString(branch)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<string?> TryReadFileAsync(string path, string branch, CancellationToken ct)
    {
        using var response = await GetAsync(
            $"repository/files/{Uri.EscapeDataString(path)}/raw?ref={Uri.EscapeDataString(branch)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    // Returns a JSON array of { id, name, type, path, mode }; the name is projected. No pagination:
    // the directories read this way (.agentsmith/contexts/) hold dozens of entries at most.
    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, string branch, CancellationToken ct)
    {
        using var response = await GetAsync(
            $"repository/tree?path={Uri.EscapeDataString(path)}&ref={Uri.EscapeDataString(branch)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var names = new List<string>();
        foreach (var item in json.RootElement.EnumerateArray())
            if (item.TryGetProperty("name", out var name) && name.GetString() is { } n)
                names.Add(n);
        return names;
    }

    private async Task<HttpResponseMessage> GetAsync(string relative, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/{relative}");
        request.Headers.Add("PRIVATE-TOKEN", privateToken);
        return await httpClient.SendAsync(request, ct);
    }
}
