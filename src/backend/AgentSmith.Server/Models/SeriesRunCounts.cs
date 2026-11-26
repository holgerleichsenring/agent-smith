using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcb: a series' progress — its tickets, the landed ones, the ones whose pull request waits for a
/// merge, the held ones and the ones never started. 2026-11-25-9992a: counted from the tickets' one kind
/// (<see cref="SeriesTicketKinds"/>): waiting is now, needed or pr; held is a question, an approval (2026-11-26-5201f) or a failure; never started waits
/// with no attempt. The rest are at work, so the four need not add up to the total.</summary>
public sealed record SeriesRunCounts(int Total, int Landed, int Waiting, int Held, int NotStarted)
{
    public static SeriesRunCounts Of(IReadOnlyList<SeriesTicketFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new(facts.Count, facts.Count(f => f.Kind == SeriesTicketKinds.Merged),
            facts.Count(f => f.Kind is SeriesTicketKinds.Now or SeriesTicketKinds.Needed or SeriesTicketKinds.Pr),
            facts.Count(f => f.Kind is SeriesTicketKinds.Question or SeriesTicketKinds.Approval or SeriesTicketKinds.Draft or SeriesTicketKinds.Failed),
            facts.Count(f => f.Kind == SeriesTicketKinds.Waiting && f.LatestStatus is null));
    }
}
