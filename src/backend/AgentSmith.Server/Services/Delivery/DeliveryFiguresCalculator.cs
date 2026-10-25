using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: the key figures of a span's tickets. Merge rate and cost per merged share one denominator —
/// the tickets worked in the span, of which some merged by its end; time to merge runs from the first attempt's
/// start to the merge. Nothing over zero is a number: those figures are null.
/// </summary>
public sealed class DeliveryFiguresCalculator
{
    public DeliveryFigures Of(IReadOnlyList<DeliveryTicket> tickets)
    {
        var attempts = tickets.SelectMany(t => t.Attempts).ToList();
        var merged = tickets.Where(t => t.MergedAt is not null).ToList();
        var spend = attempts.Sum(a => a.Row.CostUsd);
        return new DeliveryFigures(
            tickets.Count, attempts.Count, merged.Count,
            Ratio(merged.Count, tickets.Count),
            merged.Select(t => (t.MergedAt!.Value - t.First.Row.StartedAt).TotalSeconds).MedianOrNull(),
            Ratio(attempts.Count(a => a.Rework), tickets.Count),
            spend,
            merged.Count == 0 ? null : spend / merged.Count,
            attempts.Sum(a => a.Seconds));
    }

    private static double? Ratio(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
