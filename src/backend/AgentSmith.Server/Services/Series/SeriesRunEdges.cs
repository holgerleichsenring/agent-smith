using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-23-31dcf: the requires edges between two tickets of a series, from the facts already read for it.
/// 2026-11-23-f562: the edge kind and the per-repo stack read behind it are gone — nothing draws them any more.
/// </summary>
public static class SeriesRunEdges
{
    public static IReadOnlyList<SeriesRunEdgeView> Of(SeriesRunProjectFacts facts, IReadOnlyCollection<string> ticketIds)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var series = ticketIds.ToHashSet(StringComparer.Ordinal);
        return [.. facts.Edges.Where(p => series.Contains(p.Key)).SelectMany(p => p.Value
            .Where(e => series.Contains(e.RequiredTicketId)).DistinctBy(e => e.RequiredTicketId, StringComparer.Ordinal)
            .Select(e => new SeriesRunEdgeView(e.TicketId, e.RequiredTicketId)))];
    }
}
