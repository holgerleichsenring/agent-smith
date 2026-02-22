using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Creates the appropriate ITicketProvider based on configuration.
/// </summary>
public interface ITicketProviderFactory
{
    ITicketProvider Create(TicketConfig config);
}
