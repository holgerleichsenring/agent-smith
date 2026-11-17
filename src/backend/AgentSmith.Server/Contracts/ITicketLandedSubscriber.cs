using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// 2026-11-17-023fb: something that acts when a ticket lands. Called once per landing, by the replica whose landing
/// insert won, after its dependents were woken — every subscriber in turn, a throw logged and the next still called.
/// A subscriber returns quickly: work that takes a sandbox or a host round trip is started, not awaited.
/// </summary>
public interface ITicketLandedSubscriber
{
    Task LandedAsync(TicketLandingFact landing, CancellationToken cancellationToken);
}
