namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-9992a: what the series is working on now — the keys of its tickets a run is at work on
/// (<see cref="SeriesTicketFact.Working"/>), in the series' order. The Overview highlights them; the master reads them.</summary>
public static class SeriesWorkingOn
{
    public static IReadOnlyList<string> Of(IReadOnlyList<SeriesTicketFact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return [.. facts.Where(f => f.Working).Select(f => f.Ticket.Key)];
    }
}
