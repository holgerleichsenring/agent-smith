namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-25-9992f: how the series master changes its series' cut — a ticket added through the 'file' run, joining the
/// series, and requirement edges added or removed between its tickets. Tickets are named by their tracker ids; each answer
/// is the sentence of what happened. The edges refuse only what the graph cannot hold: a ticket requiring itself or a cycle.
/// </summary>
public interface ISeriesEditActs
{
    /// <summary>Files one ticket from <paramref name="title"/> and <paramref name="body"/> into the series, unstarted; it
    /// requires <paramref name="requires"/>, and each of <paramref name="requiredBy"/> requires it.</summary>
    Task<SeriesCutAnswer> AddTicketAsync(SeriesRunRecord series, string title, string body, IReadOnlyList<string> requires,
        IReadOnlyList<string> requiredBy, CancellationToken cancellationToken);

    /// <summary>Adds the edges by which <paramref name="ticketId"/> requires <paramref name="addRequires"/> and removes those to
    /// <paramref name="removeRequires"/>; nothing is changed when an added edge would require itself or close a cycle.</summary>
    Task<SeriesCutAnswer> LinkAsync(SeriesRunRecord series, string ticketId, IReadOnlyList<string> addRequires,
        IReadOnlyList<string> removeRequires, CancellationToken cancellationToken);
}
