using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a series' progress — its tickets, the landed ones, the ones whose pull request waits for a
/// merge, the held ones and the ones never started. 2026-11-25-9992a: counted from the tickets' one kind
/// (<see cref="SeriesTicketKinds"/>): waiting is now, needed or pr; held is a question, an approval (2026-11-26-5201f) or a failure; never started waits
/// with no attempt; 2026-11-27-3532b: a question its series master holds is not held. The rest are at work, so the four need not add up to the total.</summary>
public sealed record SeriesRunCounts(int Total, int Landed, int Waiting, int Held, int NotStarted)
{
    public static SeriesRunCounts Of(IReadOnlyList<SeriesTicketFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new(facts.Count, facts.Count(f => f.Kind == SeriesTicketKinds.Merged),
            facts.Count(f => f.Kind is SeriesTicketKinds.Now or SeriesTicketKinds.Needed or SeriesTicketKinds.Pr),
            facts.Count(f => f.Kind is SeriesTicketKinds.Approval or SeriesTicketKinds.Draft or SeriesTicketKinds.Failed
                || f.Kind == SeriesTicketKinds.Question && f.Hold?.Addressee != TicketHoldAddressees.Nobody), // 3532b: not the master's
            facts.Count(f => f.Kind == SeriesTicketKinds.Waiting && f.LatestStatus is null));
    }
}
