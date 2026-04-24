using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Creates the platform-specific ITicketStatusTransitioner based on configuration.
/// Mirrors ITicketProviderFactory.
/// </summary>
public interface ITicketStatusTransitionerFactory
{
    ITicketStatusTransitioner Create(TicketConfig config);
}
