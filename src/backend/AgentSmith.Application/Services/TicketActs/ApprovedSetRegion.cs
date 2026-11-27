using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-22-a720b (out of the retired 'ticket-body' run, 2026-11-17-dd05b): a ticket's region as its approved set says
/// it — the filing's one renderer (<see cref="SeriesTicketRegion"/>), the set's approval conversation and the board's
/// approved-set note — so the tracker text of a ticket with approved specs is a transcript of them.
/// </summary>
public sealed class ApprovedSetRegion(SeriesTicketRegion regions)
{
    public string Render(TrackerConnection tracker, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(set);
        var stamp = TicketLabelVocabulary.For(tracker).ApprovedSetStamp;
        var retired = set.Retired(); // 2026-11-27-3532d: the ticket's text names the specs that stand
        return regions.Render(set.Goal ?? string.Empty, set.Series ?? set.Key, [.. set.Specs.Where(s => !retired.Contains(s.SpecId)).Select(s => s.Draft)],
            set.Approval?.Conversation, TicketLabelNote.For([stamp], stamp)).Body;
    }
}
