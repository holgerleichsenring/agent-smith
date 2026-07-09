using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: the routing seam for a CONFIRMED, non-answer outcome proposal.
/// The kind decides the path: bug → the fix-bug ticket shape, phase/epic →
/// phase ticket(s). p0315c replaces the default implementation with real
/// tracker filing via ITicketProvider; until then the shipped default
/// (SessionStoreOutcomeSink) stores the proposal durably and says so in the
/// thread — it never fakes a filed ticket.
/// </summary>
public interface IOutcomeSink
{
    Task AcceptAsync(ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken);
}
