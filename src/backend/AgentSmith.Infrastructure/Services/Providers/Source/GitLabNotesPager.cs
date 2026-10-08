using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-f147: a merge request's notes, newest first, paged by 100 — the one read the marker
/// delete and the act reader share. A page shorter than 100 is the last.
/// </summary>
public sealed class GitLabNotesPager(string baseUrl, string projectPath, string token, HttpClient http, ILogger logger)
{
    private const int PageSize = 100;

    public async Task<IReadOnlyList<GitLabNote>> ReadAsync(string mrIid, int maxPages, CancellationToken ct)
    {
        var notes = new List<GitLabNote>();
        for (var page = 1; page <= maxPages; page++)
        {
            var read = await PageAsync(mrIid, page, ct);
            notes.AddRange(read);
            if (read.Count < PageSize) return notes;
        }
        logger.LogWarning("MR !{Iid} has more than {Max} notes; the newest are read", mrIid, maxPages * PageSize);
        return notes;
    }

    private async Task<IReadOnlyList<GitLabNote>> PageAsync(string mrIid, int page, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests/{mrIid}/notes?sort=desc&order_by=created_at&per_page={PageSize}&page={page}");
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        await response.EnsureSuccessWithBodyAsync(ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return [.. json.RootElement.EnumerateArray().Select(Note)];
    }

    private static GitLabNote Note(JsonElement n)
    {
        var author = n.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? a : (JsonElement?)null;
        return new GitLabNote(n.GetProperty("id").GetInt64(), Str(n, "body") ?? string.Empty,
            author is { } x && x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null,
            author is { } y ? Str(y, "username") : null,
            n.TryGetProperty("system", out var s) && s.ValueKind == JsonValueKind.True,
            Str(n, "created_at") is { } at ? DateTimeOffset.Parse(at) : DateTimeOffset.MinValue);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
