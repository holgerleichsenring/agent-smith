namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-17-0207a: reads what is true of a series' member tickets, in the order given.</summary>
public interface ISeriesTicketFacts
{
    Task<IReadOnlyList<SeriesTicketFact>> ReadAsync(
        string project, IReadOnlyList<SeriesTicket> tickets, CancellationToken cancellationToken);
}
