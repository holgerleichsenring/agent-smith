using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-2123: a Jira issue's status history and the token's identity. Cloud pages the changelog
/// (values, total), read from its LAST page; a 404 there — or a Data Center pointed at the issue
/// with expand=changelog — answers changelog.histories in one read. An app account (accountType app)
/// is no person; identity is accountId, else key or name.
/// </summary>
internal sealed class JiraStatusHistory(TicketProviderHttpClient http, string baseUrl, JiraEndpoints endpoints)
    : ITicketStatusHistory, ITrackerSelf
{
    private const int PageSize = 100;

    public async Task<TrackerActor?> SelfAsync(CancellationToken cancellationToken)
    {
        using var me = await http.SendForJsonAsync(HttpMethod.Get, $"{baseUrl}{endpoints.Myself}", null, cancellationToken);
        return me is null ? null : Actor(me.RootElement);
    }

    public async Task<TicketStatusMove?> NewestPersonMoveIntoAsync(
        TicketId ticketId, IReadOnlyCollection<string> statuses, TrackerActor? self, CancellationToken cancellationToken)
    {
        var histories = await HistoriesAsync(ticketId.Value, cancellationToken);
        return histories
            .Select(h => (Actor: h.TryGetProperty("author", out var a) ? Actor(a) : null, Item: StatusInto(h, statuses), At: Time(h)))
            .Where(m => m.Item is not null && m.Actor is not null && !m.Actor.IsApp && !m.Actor.Is(self))
            .Select(m => new TicketStatusMove(m.Actor!, m.At, m.Item!))
            .MaxBy(m => m.At);
    }

    private async Task<IReadOnlyList<JsonElement>> HistoriesAsync(string key, CancellationToken ct)
    {
        var url = $"{baseUrl}{endpoints.ChangelogFor(key)}";
        if (url.Contains("expand=changelog", StringComparison.OrdinalIgnoreCase)) return await DataCenterAsync(url, ct);
        using var first = await http.SendForJsonAsync(HttpMethod.Get, $"{url}?startAt=0&maxResults={PageSize}", null, ct);
        if (first is null) return await DataCenterAsync($"{baseUrl}{endpoints.IssueFor(key)}?expand=changelog", ct);
        var total = first.RootElement.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
        if (total <= PageSize) return Values(first.RootElement, "values");
        using var last = await http.SendForJsonOrThrowAsync(HttpMethod.Get, $"{url}?startAt={total - PageSize}&maxResults={PageSize}", null, ct);
        return Values(last.RootElement, "values");
    }

    private async Task<IReadOnlyList<JsonElement>> DataCenterAsync(string url, CancellationToken ct)
    {
        using var issue = await http.SendForJsonOrThrowAsync(HttpMethod.Get, url, null, ct);
        return issue.RootElement.TryGetProperty("changelog", out var log) ? Values(log, "histories") : [];
    }

    private static IReadOnlyList<JsonElement> Values(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray().Select(x => x.Clone())] : [];

    private static string? StatusInto(JsonElement history, IReadOnlyCollection<string> statuses) =>
        Values(history, "items").Where(i => Str(i, "field") is { } f && f.Equals("status", StringComparison.OrdinalIgnoreCase))
            .Select(i => Str(i, "toString")).FirstOrDefault(s => s is not null && statuses.Contains(s, StringComparer.OrdinalIgnoreCase));

    private static TrackerActor? Actor(JsonElement user) =>
        (Str(user, "accountId") ?? Str(user, "key") ?? Str(user, "name")) is { } id
            ? new TrackerActor(id, Str(user, "name") ?? Str(user, "displayName"), Str(user, "accountType") == "app")
            : null;

    private static DateTimeOffset Time(JsonElement history) =>
        Str(history, "created") is { } at && DateTimeOffset.TryParse(JiraTime(at), out var parsed) ? parsed : DateTimeOffset.MinValue;

    // Jira writes offsets without a colon ("+0000").
    private static string JiraTime(string at) =>
        at.Length > 5 && (at[^5] == '+' || at[^5] == '-') && char.IsDigit(at[^1]) ? at[..^2] + ":" + at[^2..] : at;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
