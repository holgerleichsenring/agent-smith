using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-9e6e: a GitLab project's open merge requests updated since the cursor, oldest first —
/// a "requested changes" note touches the MR (at most once a minute, so the read starts a minute
/// early). The worker reads the act; this only says which merge requests changed.
/// <para>2026-11-25-cc1f: every open merge request, newest first and given the same page budget as the act read, is also listed RefreshOnly under a
/// name of its target, head, draft and conflict state (<see cref="PullRequestRefreshName"/>) — a conflict GitLab computes after the
/// target moved need not touch updated_at. Such an item carries the read's since, so it never moves the cursor.</para>
/// </summary>
public sealed class GitLabChangedPullRequests(string baseUrl, string projectPath, string token, HttpClient http)
{
    private const int PageSize = 100;

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var after = (since - TimeSpan.FromSeconds(60)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var items = new List<ChangedItem>();
        var cut = !await ListAsync($"state=opened&updated_after={after}&order_by=updated_at&sort=asc", maxPages, mr => items.Add(Item(mr)), ct);
        await ListAsync("state=opened&order_by=updated_at&sort=desc", maxPages, mr => items.Add(Refresh(mr, since)), ct);
        return new ChangedPage(items, Cut: cut);
    }

    // True when the listing ended within the page budget.
    private async Task<bool> ListAsync(string query, int maxPages, Action<JsonElement> take, CancellationToken ct)
    {
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/api/v4/projects/{projectPath}/merge_requests?{query}&per_page={PageSize}&page={page}");
            request.Headers.Add("PRIVATE-TOKEN", token);
            using var response = await http.SendAsync(request, ct);
            await response.EnsureSuccessWithBodyAsync(ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            foreach (var mr in doc.RootElement.EnumerateArray()) take(mr);
            if (doc.RootElement.GetArrayLength() < PageSize) return true;
        }
        return false;
    }

    private static ChangedItem Item(JsonElement mr) => new(
        DateTimeOffset.Parse(mr.GetProperty("updated_at").GetString()!, CultureInfo.InvariantCulture),
        PrUrl: mr.GetProperty("web_url").GetString(), HeadRef: mr.GetProperty("source_branch").GetString(),
        SameRepository: SameProject(mr));

    // 2026-11-25-cc1f: the merge request as it stands, named so any change to it is a new name.
    private static ChangedItem Refresh(JsonElement mr, DateTimeOffset since) => new(since,
        PrUrl: mr.GetProperty("web_url").GetString(), HeadRef: mr.GetProperty("source_branch").GetString(),
        SameRepository: SameProject(mr), RefreshOnly: true,
        Dedupe: PullRequestRefreshName.Of(mr.GetProperty("web_url").GetString(), Text(mr, "target_branch"), Text(mr, "sha"),
            mr.TryGetProperty("draft", out var d) ? d.GetRawText() : null,
            GitLabMergeRequestRead.Conflicts(mr) is { } c ? c.ToString(CultureInfo.InvariantCulture) : "unknown", []));

    private static bool SameProject(JsonElement mr) =>
        mr.GetProperty("source_project_id").GetInt64() == mr.GetProperty("target_project_id").GetInt64();

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
