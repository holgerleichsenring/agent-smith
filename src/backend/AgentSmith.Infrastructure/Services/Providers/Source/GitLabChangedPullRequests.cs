using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-9e6e: a GitLab project's open merge requests updated since the cursor, oldest first —
/// a "requested changes" note touches the MR (at most once a minute, so the read starts a minute
/// early). The worker reads the act; this only says which merge requests changed.
/// </summary>
public sealed class GitLabChangedPullRequests(string baseUrl, string projectPath, string token, HttpClient http)
{
    private const int PageSize = 100;

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var after = (since - TimeSpan.FromSeconds(60)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var items = new List<ChangedItem>();
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests?state=opened&updated_after={after}&order_by=updated_at&sort=asc&per_page={PageSize}&page={page}");
            request.Headers.Add("PRIVATE-TOKEN", token);
            using var response = await http.SendAsync(request, ct);
            await response.EnsureSuccessWithBodyAsync(ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            items.AddRange(doc.RootElement.EnumerateArray().Select(Item));
            if (doc.RootElement.GetArrayLength() < PageSize) return new ChangedPage(items);
        }
        return new ChangedPage(items, Cut: true);
    }

    private static ChangedItem Item(JsonElement mr) => new(
        DateTimeOffset.Parse(mr.GetProperty("updated_at").GetString()!, CultureInfo.InvariantCulture),
        PrUrl: mr.GetProperty("web_url").GetString(), HeadRef: mr.GetProperty("source_branch").GetString(),
        SameRepository: mr.GetProperty("source_project_id").GetInt64() == mr.GetProperty("target_project_id").GetInt64());
}
