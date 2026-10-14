namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-13-9a59a: tells every replica's dashboards that a ticket changed, so a ticket page
/// refetches it. Data-free by design: the key names the ticket and the page reads it itself.
/// </summary>
public interface ITicketActivityNudge
{
    /// <param name="ticketKey">TicketKey.For(tracker type lowercased, ticket id).Value.</param>
    Task ChangedAsync(string project, string ticketKey, CancellationToken cancellationToken);
}
