namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-9e6e: a tracker's tickets updated since a cursor, oldest first, within a page budget.</summary>
public interface IChangedTicketLister
{
    Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, int maxPages, CancellationToken cancellationToken);
}
