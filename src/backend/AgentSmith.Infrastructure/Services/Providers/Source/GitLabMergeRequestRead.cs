using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-13-9a59b: GET one merge request — who opened it and its state with their times. Shared by
/// the act reader (the author is never an act) and the state reader.
/// </summary>
public sealed class GitLabMergeRequestRead(string baseUrl, string projectPath, string token, HttpClient http)
{
    public async Task<GitLabMergeRequest> ReadAsync(int iid, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{iid}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        using var mr = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = mr.RootElement;
        var author = root.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object
            && a.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : (long?)null;
        return new GitLabMergeRequest(author, Str(root, "state") ?? "opened", Time(root, "created_at"), Time(root, "merged_at"), Time(root, "closed_at"));
    }

    private static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } at ? DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
