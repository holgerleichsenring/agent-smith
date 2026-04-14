using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// Routes incoming bus messages to the appropriate platform adapter
/// based on conversation state and message type.
/// </summary>
public interface IBusMessageRouter
{
    Task HandleAsync(BusMessage message, CancellationToken cancellationToken);
}
