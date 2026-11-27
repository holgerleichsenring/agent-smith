namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-27-3532d: the series master retires an unstarted spec of one of its tickets for the replacement it writes —
/// recorded, as every edit of a worked ticket's series is, for that ticket's next attempt; nothing starts. The answer is
/// the sentence of what happened, with the spec ids the next attempt will hold.
/// </summary>
public interface ISeriesSpecRetirement
{
    /// <summary>Retires <paramref name="specId"/> of <paramref name="ticketId"/>'s series for <paramref name="yaml"/>,
    /// which takes its place under a newly minted id.</summary>
    Task<SeriesCutAnswer> RetireAsync(
        SeriesRunRecord series, string ticketId, string specId, string yaml, CancellationToken cancellationToken);
}
