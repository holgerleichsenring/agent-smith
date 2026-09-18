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
/// <para>
/// 2026-09-17-042eg: <c>mayStartRuns</c> is whether the APPROVER holds runs.control. It rides
/// the turn in process rather than being persisted on the answer: the approving message only
/// answers the pending question, and the principal that started the turn is the one who approves.
/// Both chat channels pass false — a move that starts a run is a dashboard affordance.
/// </para>
/// </summary>
public interface IOutcomeSink
{
    Task AcceptAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken);
}
