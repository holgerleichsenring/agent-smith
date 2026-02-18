using AgentSmith.Domain.Entities;
using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides access to tickets from an external system (Azure DevOps, Jira, GitHub).
/// </summary>
public interface ITicketProvider
{
    string ProviderType { get; }

    Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a status comment to the ticket.
    /// </summary>
    Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Closes the ticket with a resolution comment.
    /// </summary>
    Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
