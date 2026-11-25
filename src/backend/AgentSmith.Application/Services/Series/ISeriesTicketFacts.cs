using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: reads what is true of a series' member tickets, in the series' order. 2026-11-25-9992a: the
/// one read every surface of a series goes through — the master's beat, the dialog panel, the series page and its list.</summary>
public interface ISeriesTicketFacts
{
    Task<IReadOnlyList<SeriesTicketFact>> ReadAsync(SeriesRunRecord record, CancellationToken cancellationToken);

    /// <summary>Several series at once — one hold read per project, as the sweep reads every active series per scan.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<SeriesTicketFact>>> ReadManyAsync(
        IReadOnlyList<SeriesRunRecord> records, CancellationToken cancellationToken);
}
