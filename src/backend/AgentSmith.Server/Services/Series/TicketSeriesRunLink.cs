using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcc: the newest series run that holds a ticket, ended ones included, with its name; null when no
/// series ever held it. Each read on a scope of its own, so the ticket page may run it beside its other reads.</summary>
public sealed class TicketSeriesRunLink(ISeriesRunStore store, IServiceScopeFactory scopes)
{
    public async Task<SeriesRunLinkView?> ForAsync(string project, string ticketId, CancellationToken ct)
    {
        if (await store.ForTicketAsync(project, ticketId, ct) is not { } record) return null;
        await using var scope = scopes.CreateAsyncScope();
        var names = await scope.ServiceProvider.GetRequiredService<SeriesRunNames>().OfAsync([record], ct);
        return new SeriesRunLinkView(record.Id, names[record.Id]);
    }
}
