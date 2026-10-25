using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: the tickets of one span — the attempts whose id time falls in it, grouped by (project, ticket)
/// — each with the earliest merge of its pull requests at or after its first attempt's start and by the span's end,
/// so a closed span's figures never move afterwards. The merge instant is the host's, as the PR record stores it.
/// </summary>
public sealed class DeliveryTicketComposer
{
    public IReadOnlyList<DeliveryTicket> Compose(
        IEnumerable<DeliveryAttempt> attempts, DeliverySpan span,
        ILookup<(string Project, string TicketId), PrReviewRecordView> records) =>
        [.. attempts.Where(a => span.Holds(a.Row.RunId))
            .GroupBy(a => (a.Row.Project, a.Row.TicketId))
            .Select(g => Ticket(g.Key, [.. g.OrderBy(a => a.Row.RunId, StringComparer.Ordinal)], span, records[g.Key]))];

    private static DeliveryTicket Ticket(
        (string Project, string TicketId) key, List<DeliveryAttempt> attempts, DeliverySpan span,
        IEnumerable<PrReviewRecordView> records)
    {
        var first = attempts[0].Row.StartedAt;
        var merged = records
            .Where(r => r.State == PrReviewStates.Merged && r.StateAt is { } at && at >= first && at <= span.End)
            .Select(r => r.StateAt)
            .Min();
        return new DeliveryTicket(key.Project, key.TicketId, attempts, merged);
    }
}
