using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Sweep;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-9e6e: GitLab issues updated since a cursor, oldest first. GitLab touches updated_at at
/// most once a minute, so the read starts a minute before the cursor.
/// </summary>
internal sealed class GitLabChangedTickets(TicketProviderHttpClient http, string baseUrl, string projectPath)
{
    private const int PageSize = 100;
    public static readonly TimeSpan TouchInterval = TimeSpan.FromSeconds(60);

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var after = (since - TouchInterval).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var items = new List<ChangedItem>();
        for (var page = 1; page <= maxPages; page++)
        {
            using var doc = await http.SendForJsonOrThrowAsync(HttpMethod.Get,
                $"{baseUrl}/api/v4/projects/{projectPath}/issues?updated_after={after}&order_by=updated_at&sort=asc&per_page={PageSize}&page={page}",
                null, ct);
            items.AddRange(doc.RootElement.EnumerateArray().Select(i => new ChangedItem(
                DateTimeOffset.Parse(i.GetProperty("updated_at").GetString()!, CultureInfo.InvariantCulture),
                TicketId: i.GetProperty("iid").GetInt64().ToString())));
            if (doc.RootElement.GetArrayLength() < PageSize) return new ChangedPage(items);
        }
        return new ChangedPage(items, Cut: true);
    }
}
