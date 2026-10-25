using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>2026-10-25-0099b: why the span's attempts ended, most frequent first; an attempt still going has no cause.</summary>
public sealed class DeliveryEndCauseCounter
{
    public IReadOnlyList<DeliveryEndCauseView> Of(IReadOnlyList<DeliveryTicket> tickets) =>
        [.. tickets.SelectMany(t => t.Attempts)
            .Where(a => a.Cause is not null)
            .GroupBy(a => a.Cause!, StringComparer.Ordinal)
            .Select(g => new DeliveryEndCauseView(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Cause, StringComparer.Ordinal)];
}
