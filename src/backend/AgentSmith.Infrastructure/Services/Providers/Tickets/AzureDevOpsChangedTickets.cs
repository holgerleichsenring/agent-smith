using System.Globalization;
using AgentSmith.Contracts.Sweep;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-9e6e: work items changed since a cursor, oldest first — WIQL on System.ChangedDate with
/// timePrecision, at most $top 200, each item's ChangedDate hydrated for the cursor.
/// </summary>
internal sealed class AzureDevOpsChangedTickets(AzureDevOpsConnectionCache connections, string project)
{
    private const int Top = 200;

    public async Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken ct)
    {
        var client = connections.CreateClient();
        var at = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.ChangedDate] >= '{at}' ORDER BY [System.ChangedDate] ASC",
        };
        var result = await client.QueryByWiqlAsync(wiql, project, timePrecision: true, top: Top, cancellationToken: ct);
        var ids = result.WorkItems?.Select(w => w.Id).ToList() ?? [];
        if (ids.Count == 0) return new ChangedPage([]);
        var items = await client.GetWorkItemsAsync(project, ids, ["System.Id", "System.ChangedDate"], cancellationToken: ct);
        return new ChangedPage([.. items.Select(Item).OrderBy(i => i.At)], Cut: ids.Count >= Top);
    }

    private static ChangedItem Item(WorkItem item) => new(
        item.Fields.TryGetValue("System.ChangedDate", out var changed) && changed is DateTime when
            ? new DateTimeOffset(DateTime.SpecifyKind(when, DateTimeKind.Utc)) : DateTimeOffset.MinValue,
        TicketId: item.Id?.ToString());
}
