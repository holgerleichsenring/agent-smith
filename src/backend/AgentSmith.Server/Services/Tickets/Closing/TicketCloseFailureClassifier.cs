using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Services.Tickets.Closing;

/// <summary>2026-11-17-e061a: the server's answer to the 'close' run's failure class — the hosts' exception types
/// live here, so <see cref="TicketCloseFailureReason"/> stays the server's.</summary>
public sealed class TicketCloseFailureClassifier : ITicketCloseFailureClassifier
{
    public string Of(Exception exception) => TicketCloseFailureReason.Of(exception);
}
