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
}
