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
    /// Lists all open tickets in the configured project.
    /// Returns an empty list if the provider does not support listing.
    /// </summary>
    Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Ticket>>(Array.Empty<Ticket>());

    /// <summary>
    /// Creates a new ticket with the given title and description.
    /// Returns the ID of the newly created ticket.
    /// </summary>
    Task<int> CreateAsync(string title, string description, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"CreateAsync is not supported by {nameof(ITicketProvider)}");

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
