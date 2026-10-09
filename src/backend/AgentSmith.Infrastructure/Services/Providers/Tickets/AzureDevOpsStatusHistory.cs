using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-08-2123: a work item's System.State changes, all pages of 200, timed by the update's
/// System.ChangedDate, and the token's identity from the connection (connectionData).
/// </summary>
internal sealed class AzureDevOpsStatusHistory(AzureDevOpsConnectionCache connections, string project)
    : ITicketStatusHistory, ITrackerSelf
{
    private const int PageSize = 200;

    public async Task<TrackerActor?> SelfAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Connection();
        if (!connection.HasAuthenticated) await connection.ConnectAsync(cancellationToken);
        return connection.AuthorizedIdentity is { } me ? new TrackerActor(me.Id.ToString(), me.DisplayName) : null;
    }

    public async Task<TicketStatusMove?> NewestPersonMoveIntoAsync(
        TicketId ticketId, IReadOnlyCollection<string> statuses, TrackerActor? self, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) return null;
        var client = connections.CreateClient();
        var updates = new List<WorkItemUpdate>();
        for (var skip = 0; ; skip += PageSize)
        {
            var page = await client.GetUpdatesAsync(project, id, top: PageSize, skip: skip, cancellationToken: cancellationToken);
            updates.AddRange(page);
            if (page.Count < PageSize) break;
        }
        return updates.Select(u => Move(u, statuses)).OfType<TicketStatusMove>().Where(m => !m.Actor.Is(self)).MaxBy(m => m.At);
    }

    private static TicketStatusMove? Move(WorkItemUpdate update, IReadOnlyCollection<string> statuses)
    {
        if (update.Fields is null || !update.Fields.TryGetValue("System.State", out var state)
            || state.NewValue?.ToString() is not { } into || !statuses.Contains(into, StringComparer.OrdinalIgnoreCase)
            || update.RevisedBy?.Id is not { } who) return null;
        var at = update.Fields.TryGetValue("System.ChangedDate", out var changed) && changed.NewValue is DateTime when
            ? new DateTimeOffset(DateTime.SpecifyKind(when, DateTimeKind.Utc)) : new DateTimeOffset(DateTime.SpecifyKind(update.RevisedDate, DateTimeKind.Utc));
        return new TicketStatusMove(new TrackerActor(who.ToString(), update.RevisedBy.Name), at, into);
    }
}
