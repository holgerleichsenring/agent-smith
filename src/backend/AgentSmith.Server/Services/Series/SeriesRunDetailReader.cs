using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: one series run as its page reads it — tickets in depth order with their pull requests and
/// CI per repo, progress and merge order — from the same set-based reads as the list; null when there is no such series
/// or its project is no longer configured.</summary>
public sealed class SeriesRunDetailReader(
    ISeriesRunStore store, TicketProjects projects, SeriesRunProjectReads reads, SeriesRunTickets tickets, SeriesRunNames names)
{
    public async Task<SeriesRunDetailView?> ReadAsync(string id, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } record || projects.Of(record.Project) is not { } project) return null;
        var shape = tickets.Of(record, await reads.ReadAsync(project, record.Tickets, ct));
        var name = (await names.OfAsync([record], ct))[record.Id];
        return new SeriesRunDetailView(record.Id, name, record.Project, record.State, record.Conversation, record.StartedAt,
            record.FinishedAt, shape.Counts, SeriesRunQuestion.Of(record), shape.Tickets, shape.MergeOrder);
    }
}
