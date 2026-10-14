namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// The bound ticket's series as its next attempt will read it: the series its attempts published, with a
/// recorded edit no attempt has worked yet merged over it. Built only for a ticket a code attempt worked.
/// </summary>
public interface ISeriesRead
{
    Task<SeriesReading> ReadAsync(CancellationToken cancellationToken);
}
