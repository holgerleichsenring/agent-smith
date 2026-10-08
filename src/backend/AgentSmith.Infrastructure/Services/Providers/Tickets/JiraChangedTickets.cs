using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-9e6e: Jira issues updated since a cursor, oldest first. JQL compares to the minute in
/// the searching user's zone, so the cursor is written in the token user's zone (/myself), floored
/// to the minute with one minute re-read; in an ambiguous DST hour it is written an hour earlier.
/// Cloud pages by nextPageToken, Data Center (/rest/api/2/search) by startAt and total. A minute that
/// holds more than the budget is read to its end. A failure throws — a partial answer would move the
/// cursor past what was never read.
/// </summary>
internal sealed class JiraChangedTickets(TicketProviderHttpClient http, string baseUrl, JiraEndpoints endpoints, string? projectKey)
{
    private const int PageSize = 100;
    private const int MinuteOverrunPages = 50;

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var jql = $"{(projectKey is null ? "" : $"project = \"{projectKey}\" AND ")}updated >= \"{await CursorAsync(since, ct)}\" ORDER BY updated ASC";
        var items = new List<ChangedItem>();
        string? token = null;
        var startAt = 0;
        for (var page = 0; ; page++)
        {
            using var doc = await http.SendForJsonOrThrowAsync(HttpMethod.Post, $"{baseUrl}{endpoints.Search}",
                Body(jql, token, startAt), ct);
            var read = Issues(doc.RootElement);
            items.AddRange(read);
            (token, startAt, var more) = Next(doc.RootElement, startAt, read.Count);
            if (!more) return new ChangedPage(items);
            if (page + 1 >= maxPages && !SameMinute(items) || page + 1 >= maxPages + MinuteOverrunPages) return new ChangedPage(items, Cut: true);
        }
    }

    private static object Body(string jql, string? token, int startAt) => token is not null
        ? new { jql, fields = new[] { "updated" }, maxResults = PageSize, nextPageToken = token }
        : new { jql, fields = new[] { "updated" }, maxResults = PageSize, startAt };

    private static (string? Token, int StartAt, bool More) Next(JsonElement root, int startAt, int read)
    {
        if (root.TryGetProperty("nextPageToken", out var t) && t.GetString() is { Length: > 0 } token) return (token, 0, true);
        var total = root.TryGetProperty("total", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0;
        return (null, startAt + read, read > 0 && startAt + read < total);
    }

    private static List<ChangedItem> Issues(JsonElement root) =>
        root.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array
            ? [.. issues.EnumerateArray().Select(i => new ChangedItem(Updated(i), TicketId: i.GetProperty("key").GetString()))]
            : [];

    private static DateTimeOffset Updated(JsonElement issue)
    {
        var at = issue.TryGetProperty("fields", out var f) && f.TryGetProperty("updated", out var u) ? u.GetString() : null;
        if (at is null) return DateTimeOffset.MinValue;
        var text = at.Length > 5 && (at[^5] == '+' || at[^5] == '-') ? at[..^2] + ":" + at[^2..] : at;
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
    }

    private static bool SameMinute(List<ChangedItem> items) =>
        items.Count > 0 && items[^1].At - items[0].At < TimeSpan.FromMinutes(1) && items[^1].At.Minute == items[0].At.Minute;

    private async Task<string> CursorAsync(DateTimeOffset since, CancellationToken ct)
    {
        using var me = await http.SendForJsonAsync(HttpMethod.Get, $"{baseUrl}{endpoints.Myself}", null, ct);
        var zoneId = me?.RootElement.TryGetProperty("timeZone", out var z) == true ? z.GetString() : null;
        var zone = zoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(since - TimeSpan.FromMinutes(1), zone).DateTime;
        if (zone.IsAmbiguousTime(local)) local = local.AddHours(-1);
        return local.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
    }
}
