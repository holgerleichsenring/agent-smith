namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-25-9992g: the server records what happened on a series ticket for its master; the master decides what
/// it needs. A series that holds the ticket (running, verifying, delivering or paused) keeps the wake until a beat hands it
/// over; a paused one hands it over at the first beat after resume.</summary>
public interface ISeriesWakes
{
    /// <summary>Appends the wake to every active or paused series of the project that holds the ticket, stamped with the
    /// time it is recorded; an identical wake still waiting is not appended twice. Answers whether a series holds the
    /// ticket — then its master answers the event, and the standalone path (a rework) is not taken.</summary>
    Task<bool> WakeAsync(string project, string ticketId, SeriesWake wake, CancellationToken cancellationToken);
}
