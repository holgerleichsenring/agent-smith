using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: one row per project of the span's tickets, in name order — its figures as the whole span
/// counts them, and the median agent time of its attempts that ended.
/// </summary>
public sealed class DeliveryProjectRows(DeliveryFiguresCalculator figures)
{
    public IReadOnlyList<DeliveryProjectView> Of(IReadOnlyList<DeliveryTicket> tickets) =>
        [.. tickets.GroupBy(t => t.Project)
            .OrderBy(g => g.Key, ConfigNames.Comparer)
            .Select(g => Row(g.Key, [.. g]))];

    private DeliveryProjectView Row(string project, IReadOnlyList<DeliveryTicket> tickets)
    {
        var of = figures.Of(tickets);
        var ended = tickets.SelectMany(t => t.Attempts).Where(a => a.Cause is not null).Select(a => a.Seconds);
        return new DeliveryProjectView(project, of.TicketsWorked, of.Merged, of.MergeRate, of.CostPerMergedUsd, ended.MedianOrNull());
    }
}
