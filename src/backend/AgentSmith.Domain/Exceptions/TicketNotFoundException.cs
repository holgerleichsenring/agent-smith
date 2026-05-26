using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// Thrown when a ticket cannot be found in the configured provider.
/// </summary>
public sealed class TicketNotFoundException : AgentSmithException
{
    public TicketNotFoundException(TicketId ticketId)
        : base($"Ticket '{ticketId.Value}' not found.") { }
}
